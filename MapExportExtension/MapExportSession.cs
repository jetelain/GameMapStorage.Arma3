using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Point = SixLabors.ImageSharp.Point;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace MapExportExtension
{
    internal sealed class MapExportSession : IDisposable
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(nint hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly PackageIndex _map;
        private readonly string _dataPath;

        private double _safeZoneX;
        private double _safeZoneY;
        private double _safeZoneW;
        private double _safeZoneH;
        private int _screenX;
        private int _screenY;
        private int _screenW;
        private int _screenH;
        private int _oneW;
        private int _oneH;
        private int _oneWPx;
        private int _oneHPx;
        private bool _isHiRes;

        public Image<Rgba32>? FullImage { get; private set; }

        public MapExportSession(string worldName, double worldSize, object?[]? cities, string title, double? offsetX, double? offsetY)
        {
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
                OriginY = (offsetY ?? 0) - worldSize
            };

            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Arma3MapExporter", "maps", _map.MapName);
            Directory.CreateDirectory(_dataPath);
        }

        public void Calibrate(double[] safeZone, double[] pA, double[] pB, int w, int h)
        {
            _safeZoneX = safeZone[0];
            _safeZoneY = safeZone[1];
            _safeZoneW = safeZone[2];
            _safeZoneH = safeZone[3];

            // We assume that the game window will not be moved or resized during the session, so we only get the position once at the start
            var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            GetClientRect(hwnd, out RECT clientRect);
            var origin = new POINT { X = 0, Y = 0 };
            ClientToScreen(hwnd, ref origin);
            _screenX = origin.X;
            _screenY = origin.Y;
            _screenW = clientRect.Right;
            _screenH = clientRect.Bottom;

            Extension.InfoMessage($"ScreenX={_screenX} ScreenY={_screenY} ScreenH={_screenH} ScreenW={_screenW}");

            var pxA = ArmaToScreen(pA);
            var pxB = ArmaToScreen(pB);

            _oneW = w;
            _oneH = h;
            _oneWPx = pxB.X - pxA.X;
            _oneHPx = pxA.Y - pxB.Y;

            var fullWidthInitialPx = _map.SizeInMeters * _oneWPx / w;
            var fullHeightInitialPx = _map.SizeInMeters * _oneHPx / h;
            var fullSizeInitialPx = Math.Max(fullWidthInitialPx, fullHeightInitialPx);

            if (_isHiRes)
            {
                CalibrateHiRes();
            }
            else
            {
                CalibrateInitial(fullWidthInitialPx, fullHeightInitialPx, fullSizeInitialPx);
            }
        }

        private void CalibrateInitial(double fullWidthInitialPx, double fullHeightInitialPx, double fullSizeInitialPx)
        {
            var tileSizePx = (int)Math.Ceiling(fullSizeInitialPx);

            int maxZoom = 0;
            while (tileSizePx > 400)
            {
                tileSizePx /= 2;
                maxZoom++;
            }
            tileSizePx++;

            var fullSizePx = tileSizePx * (1 << maxZoom);

            _map.TileSize = tileSizePx;
            _map.Images[0].MaxZoom = maxZoom;
            _map.DefaultZoom = Math.Max(2, maxZoom / 2);

            var adjustedWorldWidth = fullSizePx * _map.SizeInMeters / fullWidthInitialPx;
            var adjustedWorldHeight = fullSizePx * _map.SizeInMeters / fullHeightInitialPx;

            _map.FactorX = tileSizePx / adjustedWorldWidth;
            _map.FactorY = tileSizePx / adjustedWorldHeight;

            WriteIndexJson();

            FullImage?.Dispose();
            FullImage = new Image<Rgba32>(fullSizePx, fullSizePx, new Rgba32(221, 221, 221));
        }

        private void CalibrateHiRes()
        {
            var hiresMinZoom = _map.Images[0].MaxZoom + 1;
            var fullSizePx = _map.TileSize * (1 << hiresMinZoom);

            FullImage?.Dispose();
            FullImage = new Image<Rgba32>(fullSizePx, fullSizePx, new Rgba32(221, 221, 221));
        }

        public void HiResStart()
        {
            _isHiRes = true;
            FullImage?.Dispose();
            FullImage = null;
        }

        public void Stop()
        {
            if (FullImage != null)
            {
                FullImage.SaveAsPng(Path.Combine(_dataPath, "base.png"));
                FullImage.Dispose();
                FullImage = null;
            }
        }

        public void HiResStop()
        {
            if (FullImage != null)
            {
                FullImage.SaveAsPng(Path.Combine(_dataPath, "hires.png"));
                FullImage.Dispose();
                FullImage = null;

                if (!_map.Images.Any(i => i.FileName == "hires.png"))
                {
                    var hiresMinZoom = _map.Images[0].MaxZoom + 1;
                    _map.Images = [.. _map.Images, new PackageImage(hiresMinZoom, hiresMinZoom, "hires.png")];
                    WriteIndexJson();
                }
            }
        }

        public void ScreenShot(int x, int y, double[] pA, double[] pB)
        {
            if (FullImage == null)
            {
                return;
            }

            var pxA = ArmaToScreen(pA);
            var pxB = ArmaToScreen(pB);

            var crop = new Rectangle(pxA.X, pxB.Y, _oneWPx, _oneHPx);
            var point = new Point((x / _oneW) * _oneWPx, FullImage.Height - ((y / _oneH) * _oneHPx) - _oneHPx);
            using var data = TakeScreenShot();
            data.Mutate(i => i.Crop(crop));
            FullImage.Mutate(i => i.DrawImage(data, point, 1f));
        }

        public void PackAndUpload()
        {
            Task.Run(() =>
            {
                try
                {
                    var zipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Arma3MapExporter", "maps", _map.MapName + ".zip");
                    using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                    zip.CreateEntryFromFile(Path.Combine(_dataPath, "index.json"), "index.json");
                    foreach (var img in _map.Images)
                    {
                        zip.CreateEntryFromFile(Path.Combine(_dataPath, img.FileName), img.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Extension.ErrorMessage($"Unable to generate archive: {ex.Message}");
                }
                // TODO: upload to server

                Extension.Callback("Complete", _map.MapName);
            });
        }

        public void Dispose()
        {
            FullImage?.Dispose();
            FullImage = null;
        }

        private void WriteIndexJson()
        {
            var json = JsonSerializer.Serialize(_map, PackageIndexContext.Default.PackageIndex);
            File.WriteAllText(Path.Combine(_dataPath, "index.json"), json);
        }

        private Point ArmaToScreen(double[] point)
        {
            return new Point(
                (int)Math.Floor((point[0] - _safeZoneX) * _screenW / _safeZoneW),
                (int)Math.Ceiling((point[1] - _safeZoneY) * _screenH / _safeZoneH));
        }

        private Image TakeScreenShot()
        {
            using var bitmap = new System.Drawing.Bitmap(_screenW, _screenH);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(new System.Drawing.Point(_screenX, _screenY), System.Drawing.Point.Empty, new System.Drawing.Size(_screenW, _screenH));
            }
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Image.Load(ms.ToArray());
        }
    }
}
