using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaLink;

internal static class EorzeaCollectionUrls
{
    private const string GlamSearchBase = "https://ffxiv.eorzeacollection.com/glamours";

    private static readonly Dictionary<string, string> SlotFilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Head"] = "headPiece",
        ["Body"] = "bodyPiece",
        ["Hands"] = "handsPiece",
        ["Legs"] = "legsPiece",
        ["Feet"] = "feetPiece",
        ["MainHand"] = "weaponPiece",
        ["OffHand"] = "offhandPiece",
        ["Ears"] = "earringsPiece",
        ["Neck"] = "necklacePiece",
        ["Wrists"] = "braceletsPiece",
        ["Ring"] = "ringPiece",
    };

    public static string? GlamSearchUrl(int ecPieceId, string slot)
    {
        if (ecPieceId <= 0 || !SlotFilterKeys.TryGetValue(slot, out var filterKey))
            return null;

        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["filter[orderBy]"] = "loves",
            ["filter[datePeriod]"] = "any",
            ["filter[gender]"] = "any",
            ["filter[server]"] = "any",
            ["search"] = "",
            ["author"] = "",
            ["filter[class]"] = "",
            ["filter[style]"] = "",
            ["filter[theme]"] = "",
            ["filter[color]"] = "",
            ["filter[job]"] = "all",
            ["filter[minimumLvl]"] = "1",
            ["filter[maximumLvl]"] = "100",
            ["filter[headPiece]"] = "",
            ["filter[bodyPiece]"] = "",
            ["filter[handsPiece]"] = "",
            ["filter[legsPiece]"] = "",
            ["filter[feetPiece]"] = "",
            ["filter[weaponPiece]"] = "",
            ["filter[offhandPiece]"] = "",
            ["filter[earringsPiece]"] = "",
            ["filter[necklacePiece]"] = "",
            ["filter[braceletsPiece]"] = "",
            ["filter[ringPiece]"] = "",
            ["filter[fashionPiece]"] = "",
            ["filter[facePiece]"] = "",
            ["filter[save]"] = "",
            ["page"] = "1",
        };

        query[$"filter[{filterKey}]"] = ecPieceId.ToString();

        return $"{GlamSearchBase}?{string.Join("&", query.Select(static kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"))}";
    }
}
