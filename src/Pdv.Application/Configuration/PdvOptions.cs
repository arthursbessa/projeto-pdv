namespace Pdv.Application.Configuration;

public sealed class PdvOptions
{
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string? ApiToken { get; set; }
    public int SyncIntervalSeconds { get; set; } = 30;
}
