namespace EorzeaLink;

public sealed class ResolvedRow
{
    public string Slot { get; init; }
    public string ItemName { get; init; }
    public int ItemId { get; init; }
    public uint? Stain1Id { get; init; }
    public uint? Stain2Id { get; init; }
    public string? Stain1Name { get; init; }
    public string? Stain2Name { get; init; }

    // ownership
    public Ownership.OwnStatus Own { get; set; } = Ownership.OwnStatus.Unknown;
    public string OwnSource { get; set; } = "—";

    public ResolvedRow(
        string slot,
        string itemName,
        int itemId,
        uint? s1,
        uint? s2 = null,
        string? stain1Name = null,
        string? stain2Name = null)
        => (Slot, ItemName, ItemId, Stain1Id, Stain2Id, Stain1Name, Stain2Name)
            = (slot, itemName, itemId, s1, s2, stain1Name, stain2Name);
}
