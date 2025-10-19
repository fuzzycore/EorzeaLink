namespace EorzeaLink;

public sealed class ResolvedRow
{
    public string Slot { get; init; }
    public string ItemName { get; init; }
    public int ItemId { get; init; }
    public uint? Stain1Id { get; init; }
    public uint? Stain2Id { get; init; }

    // ownership
    public Ownership.OwnStatus Own { get; set; } = Ownership.OwnStatus.Unknown;
    public string OwnSource { get; set; } = "—";

    public ResolvedRow(string slot, string itemName, int itemId, uint? s1, uint? s2 = null)
        => (Slot, ItemName, ItemId, Stain1Id, Stain2Id) = (slot, itemName, itemId, s1, s2);
}
