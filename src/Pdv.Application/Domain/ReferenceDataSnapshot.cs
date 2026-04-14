namespace Pdv.Application.Domain;

public sealed class ReferenceDataSnapshot
{
    public IReadOnlyCollection<LookupOption> Categories { get; init; } = [];
    public IReadOnlyCollection<LookupOption> Suppliers { get; init; } = [];
}
