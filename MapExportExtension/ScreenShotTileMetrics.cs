using SixLabors.ImageSharp;

namespace MapExportExtension
{
    internal class ScreenShotTileMetrics
    {
        public ScreenShotTileMetrics(double meters, int fullSizePx, double adjustedWorldSize)
        {
            Meters = meters;
            PixelExact = fullSizePx / adjustedWorldSize * Meters;
            Pixel = (int)Math.Ceiling(PixelExact);

            FullSizeMeters = adjustedWorldSize;
            FullSizePixels = fullSizePx;
        }

        public double Meters { get; }
        public double PixelExact { get; }
        public int Pixel { get; }
        public int FullSizePixels { get; }
        public double FullSizeMeters { get; }

        public override string ToString()
        {
            return $"Tile={Meters} m, {Pixel} px ({PixelExact:F2}) World={FullSizeMeters:F1} m, {FullSizePixels} px Resolution={Pixel / Meters:F2} px/m";
        }

        public Point GetTileTopLeft(double xMeters, double yMeters)
        {
            return new Point((int)(xMeters / Meters) * Pixel, FullSizePixels - ((int)(yMeters / Meters) * Pixel) - Pixel);
        }
    }
}
