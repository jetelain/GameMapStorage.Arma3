using System.Diagnostics;
using System.Globalization;
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
    internal static class Worker
    {
        private static PackageIndex? currentMap;
        private static string? mapDataPath;

        private static double SafeZoneX;
        private static double SafeZoneY;
        private static double SafeZoneW;
        private static double SafeZoneH;

        private static int ScreenW;
        private static int ScreenH;

        private static int OneW;
        private static int OneH; 
        private static int OneWPx;
        private static int OneHPx;

        private static bool IsHiRes;

        public static Image<Rgba32>? FullImage { get; private set; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        internal static void Message(string function, string[] args)
        {
            switch (function)
            {
                case "start":
                    Start(ArmaSerializer.ParseString(args[0]), 
                        double.Parse(args[1], CultureInfo.InvariantCulture), 
                        ArmaSerializer.ParseMixedArray(args[2]), 
                        ArmaSerializer.ParseDoubleArray(args[3]),
                        ArmaSerializer.ParseString(args[4]),
                        ArmaSerializer.ParseDouble(args[5]),
                        ArmaSerializer.ParseDouble(args[6]));
                    return;
                case "histart":
                    HiResStart();
                    return;
                case "calibrate":
                    Calibrate(ArmaSerializer.ParseDoubleArray(args[0]),
                        ArmaSerializer.ParseDoubleArray(args[1]),
                        ArmaSerializer.ParseDoubleArray(args[2]),
                        int.Parse(args[3]), 
                        int.Parse(args[4]));
                    return;
                case "screenshot":
                    ScreenShot(int.Parse(args[0]), 
                        int.Parse(args[1]),
                        ArmaSerializer.ParseDoubleArray(args[2]),
                        ArmaSerializer.ParseDoubleArray(args[3]));
                    return;
                case "stop":
                    Stop();
                    return;
                case "histop":
                    HiResStop();
                    return;
                case "dispose":
                    Dispose();
                    return;
            }
        }

        private static void Dispose()
        {
            if (FullImage != null)
            {
                FullImage.Dispose();
                FullImage = null;
            }
            mapDataPath = null;
            currentMap = null;
            IsHiRes = false;
        }

        private static void HiResStop()
        {
            if (FullImage != null)
            {
                if (mapDataPath != null)
                {
                    FullImage.SaveAsPng(Path.Combine(mapDataPath, "hires.png"));
                }
                FullImage.Dispose();
                FullImage = null;
            }
        }

        private static void HiResStart()
        {
            IsHiRes = true;
            if (FullImage != null)
            {
                FullImage.Dispose();
                FullImage = null;
            }
        }

        private static void Stop()
        {
            if (FullImage != null)
            {
                if (mapDataPath != null)
                {
                    FullImage.SaveAsPng(Path.Combine(mapDataPath, "base.png"));
                }
                FullImage.Dispose();
                FullImage = null;
            }
        }

        private static void Start(string worldName, double worldSize, object[] cities, double[] center, string title, double? offsetX, double? offsetY)
        {
            IsHiRes = false;
            currentMap = new PackageIndex()
            {
                GameName="arma3",
                SizeInMeters = worldSize,
                MapName = worldName.ToLowerInvariant(),
                EnglishTitle = title,
                Locations = cities.Cast<object[]>().Select(c => new PackageLocation((string)c[0], 0, (double)((object[])c[1])[0], (double)((object[])c[1])[1])).ToArray(),
                Images = [ 
                    new PackageImage(0, 1, "base.png"), 
                    new PackageImage(2, 2, "hires.png")
                    ],
                Culture = string.Empty,
                OriginX = (offsetX ?? 0),
                OriginY = (offsetY  ?? 0) - worldSize
            };
            mapDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Arma3MapExporter", "maps", currentMap.MapName);
            if (!Directory.Exists(mapDataPath))
            {
                Directory.CreateDirectory(mapDataPath);
            }
        }

        private static void Calibrate(double[] safeZone, double[] pA, double[] pB, int w, int h)
        {
            if (/*Screen.PrimaryScreen == null ||*/ currentMap == null)
            {
                Extension.ErrorMessage("No currentMap");
                return;
            }
            SafeZoneX = safeZone[0];
            SafeZoneY = safeZone[1];
            SafeZoneW = safeZone[2];
            SafeZoneH = safeZone[3];

            GetWindowRect(Process.GetCurrentProcess().MainWindowHandle, out RECT lpRect);
            ScreenH = lpRect.Bottom; // Screen.PrimaryScreen.Bounds.Height;
            ScreenW = lpRect.Right; // Screen.PrimaryScreen.Bounds.Width;

            Extension.DebugMessage($"ScreenH={ScreenH} ScreenW={ScreenW}");

            var pxA = ArmaToScreen(pA);
            var pxB = ArmaToScreen(pB);

            OneW = w;
            OneH = h;

            OneWPx = (pxB.X - pxA.X);
            OneHPx = (pxA.Y - pxB.Y);

            var fullWidthInitialPx = currentMap.SizeInMeters * OneWPx / w;
            var fullHeightInitialPx = currentMap.SizeInMeters * OneHPx / h;
            var fullSizeInitialPx = Math.Max(fullWidthInitialPx, fullHeightInitialPx);

            if (IsHiRes)
            {
                CalibrateHiRes(fullWidthInitialPx, fullHeightInitialPx, fullSizeInitialPx);
            }
            else
            {
                CalibrateInitial(fullWidthInitialPx, fullHeightInitialPx, fullSizeInitialPx);
            }
        }

        private static void CalibrateInitial(double fullWidthInitialPx, double fullHeightInitialPx, double fullSizeInitialPx)
        {
            if (mapDataPath == null || currentMap == null)
            {
                return;
            }

            var tileSizePx = (int)Math.Ceiling(fullSizeInitialPx);

            int maxZoom = 0;
            while (tileSizePx > 400)
            {
                tileSizePx = tileSizePx / 2;
                maxZoom++;
            }
            tileSizePx++;

            var fullSizePx = tileSizePx * (1 << maxZoom);

            currentMap.TileSize = tileSizePx;
            currentMap.Images[0].MaxZoom = maxZoom;
            currentMap.Images[1].MinZoom = maxZoom + 1;
            currentMap.Images[1].MaxZoom = maxZoom + 1;
            currentMap.DefaultZoom = Math.Max(2, maxZoom / 2);


            var adjustedWorldWidth = fullSizePx * currentMap.SizeInMeters / fullWidthInitialPx;
            var adjustedWorldHeight = fullSizePx * currentMap.SizeInMeters / fullHeightInitialPx;

            var coefWidth = tileSizePx / adjustedWorldWidth;
            var coefHeight = tileSizePx / adjustedWorldHeight;

            currentMap.FactorX = coefWidth;
            currentMap.FactorY = coefHeight;

            var json = JsonSerializer.Serialize(currentMap, PackageIndexContext.Default.PackageIndex);

            File.WriteAllText(Path.Combine(mapDataPath, $"index.json"), json);

            FullImage = new Image<Rgba32>(fullSizePx, fullSizePx, new Rgba32(221, 221, 221));
        }

        private static void CalibrateHiRes(double fullWidthInitialPx, double fullHeightInitialPx, double fullSizeInitialPx)
        {
            if (mapDataPath == null || currentMap == null)
            {
                return;
            }
            var fullSizePx = currentMap.TileSize * (1 << currentMap.Images[1].MinZoom);
            FullImage = new Image<Rgba32>(fullSizePx, fullSizePx, new Rgba32(221, 221, 221));
        }


        private static void ScreenShot(int x, int y, double[] pA, double[] pB)
        {
            if (mapDataPath == null || currentMap == null || FullImage == null)
            {
                return;
            }
            var pxA = ArmaToScreen(pA);
            var pxB = ArmaToScreen(pB);

            //Trace.TraceInformation("X={0} Y={1} W={2} (not used) H={3} (not used)", pxA.X, pxB.Y, (pxB.X - pxA.X), (pxA.Y - pxB.Y));

            var crop = new Rectangle(pxA.X, pxB.Y, OneWPx, OneHPx);
            var point = new Point((x / OneW) * OneWPx, FullImage.Height - ((y / OneH) * OneHPx) - OneHPx);
            using (var data = TakeScreenShot($"{x}-{y}"))
            {
                data.Mutate(i => i.Crop(crop));
                FullImage.Mutate(i => i.DrawImage(data, point, 1f));
            }
        }


        private static Point ArmaToScreen(double[] point)
        {
            var p = new Point(
                (int)Math.Floor((point[0] - SafeZoneX) * ScreenW / SafeZoneW),
                (int)Math.Ceiling((point[1] - SafeZoneY) * ScreenH / SafeZoneH));
            //Trace.WriteLine($"[{point[0]},{point[1]}] => [{p.X},{p.Y}]");
            return p;
        }

        private static SixLabors.ImageSharp.Image TakeScreenShot(string name)
        {
            using (var bitmap = new System.Drawing.Bitmap(ScreenW, ScreenH))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, new System.Drawing.Size(ScreenW, ScreenH));
                }
                using(var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    var bytes = ms.ToArray();
                    return Image.Load(bytes);
                }
            }
        }
    }
}
