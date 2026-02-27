namespace Pdv.Application.Domain;

public enum OutboxStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}

public sealed class OutboxEvent
{
    public Guid Id { get; init; }
    public required string Type { get; init; }
    public required string PayloadJson { get; init; }
    public OutboxStatus Status { get; init; }
    public int Attempts { get; init; }
    public DateTimeOffset? NextRetryAt { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
