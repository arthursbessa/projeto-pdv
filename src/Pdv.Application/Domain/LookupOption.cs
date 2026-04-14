namespace Pdv.Application.Domain;

public sealed class LookupOption
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ParentId { get; init; }
}
