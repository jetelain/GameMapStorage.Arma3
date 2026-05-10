namespace MapExportLauncher;

internal class SteamApiPublishedFileDetail
{
    public string publishedfileid { get; set; } = "";
    public string? title { get; set; }
    public string? file_size { get; set; }
    public string? creator { get; set; }
    public List<SteamApiChild>? children { get; set; }
}

internal class SteamApiChild
{
    public string publishedfileid { get; set; } = "";
}

internal class SteamApiResponse
{
    public List<SteamApiPublishedFileDetail>? publishedfiledetails { get; set; }
}

internal class SteamApiRoot
{
    public SteamApiResponse? response { get; set; }
}

internal class SteamApiQueryResponse
{
    public List<SteamApiPublishedFileDetail>? publishedfiledetails { get; set; }
}

internal class SteamApiQueryRoot
{
    public SteamApiQueryResponse? response { get; set; }
}

internal class SteamApiPlayerSummary
{
    public string? steamid { get; set; }
    public string? personaname { get; set; }
}

internal class SteamApiPlayerSummariesResponse
{
    public List<SteamApiPlayerSummary>? players { get; set; }
}

internal class SteamApiPlayerSummariesRoot
{
    public SteamApiPlayerSummariesResponse? response { get; set; }
}
