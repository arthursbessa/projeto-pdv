namespace Pdv.Application.Configuration;

public sealed class PdvOptions
{
    public string DatabaseRelativePath { get; set; } = "data/pdv-local.db";
    public bool ResetDatabaseOnStartup { get; set; }
    public string DatabaseFullPath { get; set; } = string.Empty;
    public string FunctionsBaseUrl { get; set; } = string.Empty;
    public string SupabaseBaseUrl { get; set; } = string.Empty;
    public string SupabaseAnonKey { get; set; } = string.Empty;
    public string TerminalToken { get; set; } = string.Empty;
}
