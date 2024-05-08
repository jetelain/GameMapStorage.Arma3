using System.Text.Json.Serialization;

namespace MapExportExtension
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(PackageIndex))]
    internal partial class PackageIndexContext : JsonSerializerContext
    {
    }
}
