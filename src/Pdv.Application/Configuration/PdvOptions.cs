namespace Pdv.Application.Configuration;

public sealed class PdvOptions
{
    public string DatabaseRelativePath { get; set; } = "data/pdv-local.db";
    public string DatabaseFullPath { get; set; } = string.Empty;
    public string FunctionsBaseUrl { get; set; } = string.Empty;
    public string SupabaseBaseUrl { get; set; } = string.Empty;
    public string SupabaseAnonKey { get; set; } = string.Empty;
    public string TerminalToken { get; set; } = string.Empty;
    public bool EnableAutoUpdateCheck { get; set; } = true;
    public string UpdateRepositoryOwner { get; set; } = "arthursbessa";
    public string UpdateRepositoryName { get; set; } = "projeto-pdv";
    public string UpdatePackagePrefix { get; set; } = "PDV-Client";
}
