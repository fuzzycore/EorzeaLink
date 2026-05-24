using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaLink;

public sealed class GlamHistoryRow
{
    public string Slot { get; set; } = "";
    public string ItemName { get; set; } = "";
    public int ItemId { get; set; }
    public uint? Stain1Id { get; set; }
    public uint? Stain2Id { get; set; }

    public static GlamHistoryRow From(ResolvedRow r) => new()
    {
        Slot = r.Slot,
        ItemName = r.ItemName,
        ItemId = r.ItemId,
        Stain1Id = r.Stain1Id,
        Stain2Id = r.Stain2Id,
    };

    public ResolvedRow ToResolvedRow() =>
        new(Slot, ItemName, ItemId, Stain1Id, Stain2Id);
}

public sealed class GlamHistoryEntry
{
    public const int MaxEntries = 20;

    public string Url { get; set; } = "";
    public string? Title { get; set; }
    public string? Author { get; set; }
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public List<GlamHistoryRow> Rows { get; set; } = new();

    public List<ResolvedRow> ToResolvedRows() =>
        Rows.Select(r => r.ToResolvedRow()).ToList();
}
