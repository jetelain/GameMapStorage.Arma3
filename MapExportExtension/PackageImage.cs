namespace MapExportExtension
{
    public sealed class PackageImage
    {
        public PackageImage(int minZoom, int maxZoom, string fileName)
        {
            if (Path.GetFileName(fileName) != fileName)
            {
                throw new ArgumentException($"'{fileName}' is not a valid file name.");
            }
            MinZoom = minZoom;
            MaxZoom = maxZoom;
            FileName = fileName;
        }

        public int MinZoom { get; set; }

        public int MaxZoom { get; set; }

        public string FileName { get; }
    }
}
