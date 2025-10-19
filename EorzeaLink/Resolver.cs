using System;
using System.Collections.Generic;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Dalamud.Plugin.Services;

namespace EorzeaLink;

public static class Resolver
{
    public static List<ResolvedRow> ResolveAll(IDataManager data, List<ParsedRow> parsed)
    {
        var itemSheet = data.GetExcelSheet<Item>()!;
        var stainSheet = data.GetExcelSheet<Stain>()!;

        var rows = new List<ResolvedRow>();
        foreach (var row in parsed)
        {
            var (itemId, canon, itemRow) = ResolveItemId(itemSheet, row.ItemName);
            if (itemId == 0) continue;

            uint? s1 = row.Dye1 is { Length: > 0 } ? ResolveStainId(stainSheet, row.Dye1) : null;
            uint? s2 = row.Dye2 is { Length: > 0 } ? ResolveStainId(stainSheet, row.Dye2) : null;

            var slot = itemRow.HasValue ? InferSlot(data, itemRow.Value) : "Unknown";

            rows.Add(new ResolvedRow(slot, canon, itemId, s1, s2));
        }
        return rows;
    }

    private static (int itemId, string canonName, Item? itemRow) ResolveItemId(ExcelSheet<Item> sheet, string rawName)
    {
        var normalized = NormalizeName(rawName);
        Item hit = default;
        bool found = false;

        foreach (var i in sheet)
        {
            var name = i.Name.ToString().Trim();
            if (name.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(rawName, StringComparison.OrdinalIgnoreCase))
            { hit = i; found = true; break; }
        }
        if (!found)
        {
            foreach (var i in sheet)
            {
                var name = i.Name.ToString().Trim();
                if (name.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                { hit = i; found = true; break; }
            }
        }

        return found ? ((int)hit.RowId, hit.Name.ToString().Trim(), hit) : (0, rawName, null);
    }

    private static uint? ResolveStainId(ExcelSheet<Stain> sheet, string name)
    {
        Stain hit = default;
        bool found = false;

        foreach (var s in sheet)
        {
            var n = s.Name.ToString().Trim();
            if (StringEquals(n, name)) { hit = s; found = true; break; }
        }

        return found ? hit.RowId : (uint?)null;
    }

    private static string NormalizeName(string s)
    {
        var n = s.Trim();
        if (n.StartsWith("Augmented ", StringComparison.OrdinalIgnoreCase)) n = n[10..];
        return n.Replace("  ", " ");
    }

    private static string InferSlot(IDataManager data, Item it)
    {
        // RowRef<EquipSlotCategory> in your build
        var rr = it.EquipSlotCategory;

        // Bail if empty/invalid ref
        if (!rr.IsValid)
            return "Unknown";

        var sheet = data.GetExcelSheet<EquipSlotCategory>();
        if (sheet == null)
            return "Unknown";

        EquipSlotCategory cat;

        // GetRow can return a default struct (no backing page). Touching its properties would NRE.
        try
        {
            cat = sheet.GetRow(rr.RowId);

            // sanity ping: try to read one field to ensure backing page exists
            _ = cat.Body; // will throw NullReferenceException if default
        }
        catch
        {
            return "Unknown";
        }

        static bool F(sbyte v) => v != 0;

        try
        {
            if (F(cat.MainHand)) return "MainHand";
            if (F(cat.OffHand)) return "OffHand";
            if (F(cat.Head)) return "Head";
            if (F(cat.Body)) return "Body";
            if (F(cat.Gloves)) return "Hands";
            if (F(cat.Legs)) return "Legs";
            if (F(cat.Feet)) return "Feet";
            if (F(cat.Ears)) return "Ears";
            if (F(cat.Neck)) return "Neck";
            if (F(cat.Wrists)) return "Wrists";
            if (F(cat.FingerL) || F(cat.FingerR)) return "Ring";
            // if (F(cat.Waist)) return "Waist";
            // if (F(cat.SoulCrystal)) return "SoulCrystal";
        }
        catch
        {
            // If any property access blew up (default struct), just call it unknown.
            return "Unknown";
        }

        return "Unknown";
    }

    private static bool StringEquals(string a, string b)
        => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}

// public record ResolvedRow(string Slot, string ItemName, int ItemId, uint? Stain1Id, uint? Stain2Id = null);
