namespace Pdv.Application.Configuration;

public sealed class PdvOptions
{
    public string DatabaseRelativePath { get; set; } = "data/pdv-local.db";
    public string DatabaseFullPath { get; set; } = string.Empty;
}
