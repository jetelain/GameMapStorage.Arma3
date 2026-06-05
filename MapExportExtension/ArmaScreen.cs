using System.Runtime.InteropServices;
using Image = SixLabors.ImageSharp.Image;
using Point = SixLabors.ImageSharp.Point;

namespace MapExportExtension
{
    internal class ArmaScreen
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

        private readonly double _safeZoneX;
        private readonly double _safeZoneY;
        private readonly double _safeZoneW;
        private readonly double _safeZoneH;
        private readonly int _screenX;
        private readonly int _screenY;
        private readonly int _screenW;
        private readonly int _screenH;

        public ArmaScreen(double[] safeZone)
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
        }

        public override string ToString()
        {
            return $"ScreenX={_screenX} ScreenY={_screenY} ScreenH={_screenH} ScreenW={_screenW}";
        }

        public Point ArmaToScreen(double[] point)
        {
            return new Point(
                (int)Math.Floor((point[0] - _safeZoneX) * _screenW / _safeZoneW),
                (int)Math.Ceiling((point[1] - _safeZoneY) * _screenH / _safeZoneH));
        }

        public Image TakeScreenShot()
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
