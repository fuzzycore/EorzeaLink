using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaLink;

internal static class GlamHistory
{
    public static IReadOnlyList<GlamHistoryEntry> Entries =>
        Plugin.Cfg.GlamHistory;

    public static void Record(string url, string? title, string? author, IReadOnlyList<ResolvedRow> rows)
    {
        if (string.IsNullOrWhiteSpace(url) || rows.Count == 0)
            return;

        var normalized = url.Trim();
        var list = Plugin.Cfg.GlamHistory;

        list.RemoveAll(e => string.Equals(e.Url, normalized, StringComparison.OrdinalIgnoreCase));

        list.Insert(0, new GlamHistoryEntry
        {
            Url = normalized,
            Title = title,
            Author = author,
            RetrievedAt = DateTime.UtcNow,
            Rows = rows.Select(GlamHistoryRow.From).ToList(),
        });

        while (list.Count > GlamHistoryEntry.MaxEntries)
            list.RemoveAt(list.Count - 1);

        Plugin.Save();
    }
}
