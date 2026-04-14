namespace Pdv.Application.Domain;

public sealed class StoreSettings
{
    public string StoreName { get; set; } = "LOJA";
    public string TerminalName { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public string Currency { get; set; } = "BRL";
    public string LogoUrl { get; set; } = string.Empty;
    public string LogoLocalPath { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
