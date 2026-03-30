namespace Pdv.Ui.ViewModels;

public sealed class CustomerLookupItemViewModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Cpf { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayText => string.IsNullOrWhiteSpace(Cpf) ? Name : $"{Name} - {Cpf}";
}
