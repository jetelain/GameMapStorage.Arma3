﻿namespace MapExportExtension
{
    public sealed class PackageIndex
    {
        public required string GameName { get; set; }
        public required string MapName { get; set; }
        public double SizeInMeters { get; set; }
        public string? Culture { get; set; }
        public int Type { get; set; } = 0; // Topographic
        public string? EnglishTitle { get; set; }
        public required PackageImage[] Images { get; set; }
        public int DefaultZoom { get; set; }
        public double FactorX { get; set; }
        public double FactorY { get; set; }
        public int TileSize { get; set; }
        public required PackageLocation[] Locations { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
    }
}