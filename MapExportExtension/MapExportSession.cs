using System.IO.Compression;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Point = SixLabors.ImageSharp.Point;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace MapExportExtension
{
    internal sealed class MapExportSession : IDisposable
    {
        private readonly PackageIndex _map;
        private readonly string _dataPath;

        private ScreenShotTileMetrics? _topo;
        private bool _isHiRes;

        private PackageIndex? _aerialMap;
        private Image<Rgb24>? _aerialFullImage;
        private ScreenShotTileMetrics? _aerial;
        private Image<Rgba32>? _fullImage;
        private double _adjustedWorldSize;
        private ArmaScreen? _armaScreen;

        private readonly List<Task> _saveImageTasks = new List<Task>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        static MapExportSession()
        {
            Configuration.Default.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions()
            {
                MaximumPoolSizeMegabytes = 32_768,
                AllocationLimitMegabytes = 16_384 // a 40x40km map at 1.5px/ms is ~12GB, so 16GB limit for safety (max for aerial images)
            });
        }

        public MapExportSession(string worldName, double worldSize, object?[]? cities, string title, double? offsetX, double? offsetY)
        {
            _adjustedWorldSize = worldSize;

            _map = new PackageIndex()
            {
                GameName = "arma3",
                SizeInMeters = worldSize,
                MapName = worldName.ToLowerInvariant(),
                EnglishTitle = title,
                Locations = cities?.Cast<object[]>().Select(c => new PackageLocation((string)c[0], 0, (double)((object[])c[1])[0], (double)((object[])c[1])[1])).ToArray() ?? Array.Empty<PackageLocation>(),
                Images = [new PackageImage(0, 1, "base.png")],
                Culture = string.Empty,
                OriginX = -(offsetX ?? 0),
                OriginY = (offsetY ?? 0) - worldSize,
                SteamWorkshopId = Environment.GetEnvironmentVariable("A3ME_WORKSHOP_ID"),
                AppendAttribution = Environment.GetEnvironmentVariable("A3ME_WORKSHOP_AUTHOR") // Use steam workshop author as default attribution (can be edited later on GameMapStorage)
            };

            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Arma3MapExporter", "maps", _map.MapName);
            Directory.CreateDirectory(_dataPath);
        }

        public void InitScreen(double[] safeZone)
        {
            _armaScreen = new ArmaScreen(safeZone);
            Extension.InfoMessage(_armaScreen.ToString());
        }

        public void Calibrate(bool isHiRes, double[] pA, double[] pB, int size)
        {
            if (_armaScreen == null)
            {
                Extension.ErrorMessage("Invalid state: InitScreen was not called");
                return;
            }

            _isHiRes = isHiRes;

            var pxA = _armaScreen.ArmaToScreen(pA);
            var pxB = _armaScreen.ArmaToScreen(pB);

            if (isHiRes)
            {
                CalibrateHiRes();
            }
            else
            {
                CalibrateInitial(size, pxB.X - pxA.X, pxA.Y - pxB.Y);
            }

            _topo = new ScreenShotTileMetrics(size, _fullImage!.Width, _adjustedWorldSize);

            Extension.InfoMessage($"{TopoLayerName()}: {_topo}");
        }

        private string TopoLayerName()
        {
            return _isHiRes ? "TopoHiRes" : "TopoBase";
        }

        private void CalibrateInitial(double wantedTopoTileSizeM, double wantedTopoTileWidthPx, double wantedTopoTileHeightPx)
        {
            var fullWidthInitialPx = _map.SizeInMeters * wantedTopoTileWidthPx / wantedTopoTileSizeM;
            var fullHeightInitialPx = _map.SizeInMeters * wantedTopoTileHeightPx / wantedTopoTileSizeM;
            var fullSizeInitialPx = Math.Max(fullWidthInitialPx, fullHeightInitialPx);

            var tileSizePx = (int)Math.Ceiling(fullSizeInitialPx);
            int maxZoom = 0;
            while (tileSizePx > 400)
            {
                tileSizePx /= 2;
                maxZoom++;
            }
            tileSizePx++;

            _map.TileSize = tileSizePx;
            _map.Images[0].MaxZoom = maxZoom;
            _map.DefaultZoom = Math.Max(2, maxZoom / 2);

            var fullSizePx = tileSizePx * (1 << maxZoom);
            _adjustedWorldSize = Math.Max(fullSizePx * _map.SizeInMeters / fullWidthInitialPx, fullSizePx * _map.SizeInMeters / fullHeightInitialPx);
            _map.FactorX = _map.FactorY = tileSizePx / _adjustedWorldSize;

            _fullImage?.Dispose();
            _fullImage = new Image<Rgba32>(fullSizePx, fullSizePx, new Rgba32(221, 221, 221));
        }

        private void CalibrateHiRes()
        {
            var hiresMinZoom = GetHiresZoom();
            var fullSizePx = _map.TileSize * (1 << hiresMinZoom);

            _fullImage?.Dispose();
            _fullImage = new Image<Rgba32>(fullSizePx, fullSizePx, new Rgba32(221, 221, 221));
        }

        public void AerialCalibrate(double tileSizeM)
        {
            var aerialMaxZoom = GetAerialMaxZoom();

            var fullSizePx = _map.TileSize * (1 << aerialMaxZoom);

            _aerial = new ScreenShotTileMetrics(tileSizeM, fullSizePx, _adjustedWorldSize);

            Extension.InfoMessage($"Aerial: {_aerial}");
            _aerialFullImage?.Dispose();
            _aerialFullImage = new Image<Rgb24>(fullSizePx, fullSizePx, new Rgb24(0, 0, 0));
        }

        private int GetAerialMaxZoom()
        {
            return _map.Images[0].MaxZoom + 2;
        }

        public void AerialScreenShot(int x, int y, double[] pA, double[] pB, double[] pC, double[] pD)
        {
            if (_aerialFullImage == null || _aerial == null || _armaScreen == null)
            {
                Extension.ErrorMessage("Invalid state: AerialCalibrate or InitScreen was not called");
                return;
            }

            // D -- B
            // |    |
            // A -- C
            var pxA = _armaScreen.ArmaToScreen(pA); // SW corner
            var pxB = _armaScreen.ArmaToScreen(pB); // NE corner
            var pxC = _armaScreen.ArmaToScreen(pC); // SE corner
            var pxD = _armaScreen.ArmaToScreen(pD); // NW corner

            AerialScreenShotRectified(x, y, pxA, pxB, pxC, pxD);
        }

        private void AerialScreenShotRectified(int x, int y, Point pxA, Point pxB, Point pxC, Point pxD)
        {
            if (_aerialFullImage == null || _aerial == null || _armaScreen == null)
            {
                Extension.ErrorMessage("Invalid state");
                return;
            }

            // Bounding box of the screen quad for cropping
            var cropLeft = Math.Min(Math.Min(pxA.X, pxB.X), Math.Min(pxC.X, pxD.X));
            var cropTop = Math.Min(Math.Min(pxA.Y, pxB.Y), Math.Min(pxC.Y, pxD.Y));
            var cropRight = Math.Max(Math.Max(pxA.X, pxB.X), Math.Max(pxC.X, pxD.X));
            var cropBottom = Math.Max(Math.Max(pxA.Y, pxB.Y), Math.Max(pxC.Y, pxD.Y));
            var crop = new Rectangle(cropLeft, cropTop, cropRight - cropLeft, cropBottom - cropTop);

            Extension.InfoMessage($"Aerial: X={x} Y={y} Crop={crop}");

            // Quad corners expressed relative to the cropped region
            double ax = pxA.X - cropLeft, ay = pxA.Y - cropTop; // SW
            double bx = pxB.X - cropLeft, by = pxB.Y - cropTop; // NE
            double cx = pxC.X - cropLeft, cy = pxC.Y - cropTop; // SE
            double dx = pxD.X - cropLeft, dy = pxD.Y - cropTop; // NW

            var tilePx = _aerial.Pixel;

            using var rawBase = _armaScreen.TakeScreenShot();
            rawBase.Mutate(i => i.Crop(crop));

            // Copy source pixels for random-access sampling
            var raw = (Image<Rgba32>)rawBase;
            var srcPixels = new Rgba32[raw.Width * raw.Height];
            raw.CopyPixelDataTo(srcPixels);
            var srcWidth = raw.Width;
            var srcHeight = raw.Height;

            // Inverse homography: output rectangle → source quad (for per-pixel inverse mapping)
            //   (0,0)             → D (NW)
            //   (tilePx, 0)       → B (NE)
            //   (0,      tilePx)  → A (SW)
            //   (tilePx, tilePx)  → C (SE)
            var hInv = ComputeHomography(
                0, 0, dx, dy,
                tilePx, 0, bx, by,
                0, tilePx, ax, ay,
                tilePx, tilePx, cx, cy);

            using var rectified = new Image<Rgba32>(tilePx, tilePx);
            rectified.ProcessPixelRows(accessor =>
            {
                for (int oy = 0; oy < tilePx; oy++)
                {
                    var row = accessor.GetRowSpan(oy);
                    for (int ox = 0; ox < tilePx; ox++)
                    {
                        var (sx, sy) = ProjectPoint(hInv, ox, oy);
                        var x0 = (int)Math.Floor(sx);
                        var y0 = (int)Math.Floor(sy);
                        var x1 = x0 + 1;
                        var y1 = y0 + 1;
                        if (x0 >= 0 && y0 >= 0 && x1 < srcWidth && y1 < srcHeight)
                        {
                            var fx = sx - x0;
                            var fy = sy - y0;
                            var c00 = srcPixels[y0 * srcWidth + x0];
                            var c10 = srcPixels[y0 * srcWidth + x1];
                            var c01 = srcPixels[y1 * srcWidth + x0];
                            var c11 = srcPixels[y1 * srcWidth + x1];
                            row[ox] = new Rgba32(
                                (byte)(c00.R * (1 - fx) * (1 - fy) + c10.R * fx * (1 - fy) + c01.R * (1 - fx) * fy + c11.R * fx * fy),
                                (byte)(c00.G * (1 - fx) * (1 - fy) + c10.G * fx * (1 - fy) + c01.G * (1 - fx) * fy + c11.G * fx * fy),
                                (byte)(c00.B * (1 - fx) * (1 - fy) + c10.B * fx * (1 - fy) + c01.B * (1 - fx) * fy + c11.B * fx * fy),
                                (byte)(c00.A * (1 - fx) * (1 - fy) + c10.A * fx * (1 - fy) + c01.A * (1 - fx) * fy + c11.A * fx * fy)
                            );
                        }
                        else if ((uint)x0 < (uint)srcWidth && (uint)y0 < (uint)srcHeight)
                        {
                            // Near the border: fall back to nearest-neighbor
                            row[ox] = srcPixels[y0 * srcWidth + x0];
                        }
                    }
                }
            });

            // Composite into full aerial image
            var point = _aerial.GetTileTopLeft(x, y);
            _aerialFullImage.Mutate(i => i.DrawImage(rectified, point, 1f));
        }

        public void AerialStop()
        {
            if (_aerialFullImage != null)
            {
                SavePngAndDisposeBackground(_aerialFullImage, Path.Combine(_dataPath, "aerial.png"));
                _aerialFullImage = null;

                _aerialMap = new PackageIndex()
                {
                    GameName = _map.GameName,
                    SizeInMeters = _map.SizeInMeters,
                    MapName = _map.MapName,
                    EnglishTitle = _map.EnglishTitle,
                    Locations = _map.Locations,
                    Images = [new PackageImage(0, GetAerialMaxZoom(), "aerial.png")],
                    Culture = _map.Culture,
                    OriginX = _map.OriginX,
                    OriginY = _map.OriginY,
                    FactorX = _map.FactorX,
                    FactorY = _map.FactorY,
                    DefaultZoom = _map.DefaultZoom,
                    TileSize = _map.TileSize,
                    Type = 2, // Aerial
                    SteamWorkshopId = _map.SteamWorkshopId,
                    AppendAttribution = _map.AppendAttribution
                };
            }
        }

        public void Stop()
        {
            if (_fullImage != null)
            {
                SavePngAndDisposeBackground(_fullImage, Path.Combine(_dataPath, "base.png"));
                _fullImage = null;
            }
        }

        public void HiResStop()
        {
            if (_fullImage != null)
            {
                SavePngAndDisposeBackground(_fullImage, Path.Combine(_dataPath, "hires.png"));
                _fullImage = null;

                if (!_map.Images.Any(i => i.FileName == "hires.png"))
                {
                    var hiresMinZoom = GetHiresZoom();
                    _map.Images = [.. _map.Images, new PackageImage(hiresMinZoom, hiresMinZoom, "hires.png")];
                }
            }
        }

        private int GetHiresZoom()
        {
            // One zoom level above the base image, so that each tile is half the size of the base tiles (e.g. 1000m → 500m)
            return _map.Images[0].MaxZoom + 1;
        }

        public void ScreenShot(int x, int y, double[] pA, double[] pB)
        {
            if (_fullImage == null || _topo == null || _armaScreen == null)
            {
                Extension.ErrorMessage("Invalid state: Calibrate or InitScreen was not called");
                return;
            }

            var pxA = _armaScreen.ArmaToScreen(pA);
            var pxB = _armaScreen.ArmaToScreen(pB);

            // Use actual screen coordinates for the crop to avoid rounding drift
            var cropLeft = Math.Min(pxA.X, pxB.X);
            var cropTop = Math.Min(pxA.Y, pxB.Y);
            var cropWidth = Math.Abs(pxB.X - pxA.X);
            var cropHeight = Math.Abs(pxB.Y - pxA.Y);
            var crop = new Rectangle(cropLeft, cropTop, cropWidth, cropHeight);

            var point = _topo.GetTileTopLeft(x, y);

            using var data = _armaScreen.TakeScreenShot();
            data.Mutate(i => i.Crop(crop));

            Extension.InfoMessage($"{TopoLayerName()}: X={x} Y={y} Crop={crop}");

            // Resize to the calibrated tile size to correct any per-call pixel-level variation.
            if (cropWidth != _topo.Pixel || cropHeight != _topo.Pixel)
            {
                data.Mutate(i => i.Resize(_topo.Pixel, _topo.Pixel, KnownResamplers.Lanczos3));
            }

            _fullImage.Mutate(i => i.DrawImage(data, point, 1f));
        }

        public void Pack()
        {
            Task.Run(async () =>
            {
                await _semaphore.WaitAsync().ConfigureAwait(false); // Protect against concurrent access to _saveImageTasks list
                try
                {
                    await Task.WhenAll(_saveImageTasks).ConfigureAwait(false); // Wait for all PNG to be saved before generating the package
                }
                catch(Exception ex)
                {
                    Extension.ErrorMessage($"Error while saving images: {ex.Message}");
                    return;
                }
                finally
                {
                    _semaphore.Release();
                }

                GeneratePackage(_map, "index.json", _map.MapName + ".zip");

                if (_aerialMap != null)
                {
                    GeneratePackage(_aerialMap, "index_aerial.json", _aerialMap.MapName + "_aerial.zip");
                }

                Extension.Callback("Complete", _map.MapName);
            });
        }

        private void SavePngAndDisposeBackground(Image image, string fileName)
        {
            _semaphore.Wait(); // Protect against concurrent access to _saveImageTasks list
            try
            {
                _saveImageTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        image.SaveAsPng(Path.Combine(_dataPath, fileName));
                        image.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Extension.ErrorMessage($"Unable to save image {fileName}: {ex.Message}");
                    }
                }));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void GeneratePackage(PackageIndex pack, string indexFileName, string packageFileName)
        {
            try
            {
                var zipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Arma3MapExporter", "maps", packageFileName);
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

                WriteIndexJson(indexFileName, pack);
                zip.CreateEntryFromFile(Path.Combine(_dataPath, indexFileName), "index.json");

                foreach (var img in pack.Images)
                {
                    zip.CreateEntryFromFile(Path.Combine(_dataPath, img.FileName), img.FileName);
                }
            }
            catch (Exception ex)
            {
                Extension.ErrorMessage($"Unable to generate archive: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _fullImage?.Dispose();
            _fullImage = null;
            _aerialFullImage?.Dispose();
            _aerialFullImage = null;
        }

        private void WriteIndexJson(string fileName, PackageIndex packageIndex)
        {
            var json = JsonSerializer.Serialize(packageIndex, PackageIndexContext.Default.PackageIndex);
            File.WriteAllText(Path.Combine(_dataPath, fileName), json);
        }

        // --- Projective / homography helpers  ---

        private static double[] BasisToPoints(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
        {
            double[] m = [x1, x2, x3, y1, y2, y3, 1, 1, 1];
            double[] v = MultMV(Adj(m), [x4, y4, 1]);
            return MultMM(m, [v[0], 0, 0,  0, v[1], 0,  0, 0, v[2]]);
        }

        private static double[] Adj(double[] m) =>
        [
            m[4]*m[8]-m[5]*m[7], m[2]*m[7]-m[1]*m[8], m[1]*m[5]-m[2]*m[4],
            m[5]*m[6]-m[3]*m[8], m[0]*m[8]-m[2]*m[6], m[2]*m[3]-m[0]*m[5],
            m[3]*m[7]-m[4]*m[6], m[1]*m[6]-m[0]*m[7], m[0]*m[4]-m[1]*m[3]
        ];

        private static double[] MultMM(double[] a, double[] b)
        {
            var c = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    double cij = 0;
                    for (int k = 0; k < 3; k++)
                        cij += a[3 * i + k] * b[3 * k + j];
                    c[3 * i + j] = cij;
                }
            return c;
        }

        private static double[] MultMV(double[] m, double[] v) =>
        [
            m[0]*v[0] + m[1]*v[1] + m[2]*v[2],
            m[3]*v[0] + m[4]*v[1] + m[5]*v[2],
            m[6]*v[0] + m[7]*v[1] + m[8]*v[2]
        ];

        // Compute the 3×3 homography matrix mapping (x1s,y1s)→(x1d,y1d) .. (x4s,y4s)→(x4d,y4d)
        private static double[] ComputeHomography(
            double x1s, double y1s, double x1d, double y1d,
            double x2s, double y2s, double x2d, double y2d,
            double x3s, double y3s, double x3d, double y3d,
            double x4s, double y4s, double x4d, double y4d)
        {
            var s = BasisToPoints(x1s, y1s, x2s, y2s, x3s, y3s, x4s, y4s);
            var d = BasisToPoints(x1d, y1d, x2d, y2d, x3d, y3d, x4d, y4d);
            var m = MultMM(d, Adj(s));
            var scale = 1.0 / m[8];
            for (int i = 0; i < 9; i++) m[i] *= scale;
            return m;
        }

        private static (double x, double y) ProjectPoint(double[] m, double x, double y)
        {
            var v = MultMV(m, [x, y, 1]);
            return (v[0] / v[2], v[1] / v[2]);
        }

    }
}
