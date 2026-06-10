namespace Cycling.Rider.Tracking.Domain.Outbox;

public class TransactionFile
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public byte[] FileContent { get; init; }
    public Guid FileId { get; init; }
    public bool Processed { get; set; }
    public string ContentType { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
