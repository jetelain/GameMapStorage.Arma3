namespace MapExportExtension
{
    public sealed class PackageLocation
    {
        public PackageLocation(string englishTitle, int type, double x, double y)
        {
            EnglishTitle = englishTitle;
            Type = type;
            X = x;
            Y = y;
        }

        public string EnglishTitle { get; }

        public int Type { get; }

        public double X { get; }

        public double Y { get; }
    }
}