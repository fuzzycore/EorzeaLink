using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json.Linq;

using Glamourer.Api;                            // root
using Glamourer.Api.Api;
using Glamourer.Api.IpcSubscribers;             // Actors, Designs, Application
using Glamourer.Api.Enums;                      // EquipSlot (or Slot), StainColor handling if provided

using EquipSlot = Glamourer.Api.Enums.ApiEquipSlot;
// using ApplyState = Glamourer.Api.IpcSubscribers.GetState;

namespace EorzeaLink;

public static class GlamourerBridge
{

    // Map our text slot → Glamourer slot enum
    private static readonly Dictionary<string, EquipSlot> SlotMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MainHand"] = EquipSlot.MainHand,
        ["OffHand"] = EquipSlot.OffHand,
        ["Head"] = EquipSlot.Head,
        ["Body"] = EquipSlot.Body,
        ["Hands"] = EquipSlot.Hands,
        ["Legs"] = EquipSlot.Legs,
        ["Feet"] = EquipSlot.Feet,
        ["Ears"] = EquipSlot.Ears,
        ["Neck"] = EquipSlot.Neck,
        ["Wrists"] = EquipSlot.Wrists,
        ["Ring"] = EquipSlot.RFinger,
        // ["Waist"]    = EquipSlot.Waist,     // legacy
        // SoulCrystal is generally ignored by Glamourer designs; skip.
    };


    private static int GetLocalObjectIndex()
    {
        var lp = Plugin.ClientState.LocalPlayer;
        if (lp is null) return -1;
        for (int i = 0; i < Plugin.ObjectTable.Length; i++)
        {
            var obj = Plugin.ObjectTable[i];
            if (obj != null && obj.Address == lp.Address)
                return i;
        }
        return -1;
    }

    // Which slots exist in Glamourer’s schema? Use the enum names (what your state uses)
    private static readonly HashSet<string> ApiSlotNames =
        new(Enum.GetNames(typeof(Glamourer.Api.Enums.ApiEquipSlot)), StringComparer.OrdinalIgnoreCase);

    // Find optional flag keys on a node (Apply / ApplyStain / ApplyCrest / Crest)
    private static (string? applyKey, string? applyStainKey, string? applyCrestKey, string? crestKey)
    FindFlagKeys(JObject node)
    {
        string? Find(string a, string b) =>
            node.Property(a, StringComparison.OrdinalIgnoreCase)?.Name
            ?? node.Property(b, StringComparison.OrdinalIgnoreCase)?.Name;

        var apply = Find("Apply", "DoApply");
        var applyStain = Find("ApplyStain", "DoDye");
        var applyCrest = Find("ApplyCrest", "DoCrest");
        var crest = node.Property("Crest", StringComparison.OrdinalIgnoreCase)?.Name;
        return (apply, applyStain, applyCrest, crest);
    }

    // Set a slot node to “empty”
    private static void ClearSlotNode(JObject node, string itemKey, string dye1Key, string dye2Key)
    {
        node[itemKey] = 0u;
        node[dye1Key] = 0u;
        node[dye2Key] = 0u;

        var (applyKey, applyStainKey, applyCrestKey, crestKey) = FindFlagKeys(node);
        if (applyKey != null) node[applyKey] = true;  // actively apply the empty item
        if (applyStainKey != null) node[applyStainKey] = true;  // and empty dyes
        if (applyCrestKey != null) node[applyCrestKey] = false;
        if (crestKey != null) node[crestKey] = false;
    }

    private static (JObject state, JObject equip, string equipKey, string schemaSlotKey, string itemKey, string dye1Key, string dye2Key)
    GetStateAndSchema(int objIndex)
    {
        var (ec, data) = new GetState(Plugin.Pi).Invoke(objIndex, 0u);
        if (ec != GlamourerApiEc.Success || data is not JObject root)
            throw new InvalidOperationException("Couldn’t read Glamourer state.");

        // Prepare the set of legit slot names from ApiEquipSlot enum.
        var slotNames = Enum.GetNames(typeof(EquipSlot)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Find the equipment container by looking for an object that has children named like slots.
        foreach (var prop in root.Properties())
        {
            if (prop.Value is not JObject jo) continue;
            var slotProp = jo.Properties().FirstOrDefault(p => slotNames.Contains(p.Name));
            if (slotProp?.Value is JObject slotNode)
            {
                // This slotNode’s property names are the authoritative schema keys.
                // Find the three numeric fields on it.
                // We prefer exact names if present; else we pick the first 3 numeric fields in a stable order.
                string itemKey = slotNode.Properties()
                    .Select(p => p.Name)
                    .FirstOrDefault(n => n.Equals("ItemId", StringComparison.OrdinalIgnoreCase) || n.Equals("Item", StringComparison.OrdinalIgnoreCase))
                    ?? slotNode.Properties().First(p => p.Value.Type is JTokenType.Integer).Name;

                // collect numeric props except itemKey
                var numeric = slotNode.Properties()
                    .Where(p => p.Name != itemKey && p.Value.Type is JTokenType.Integer)
                    .Select(p => p.Name)
                    .ToList();

                // Prefer known dye names if present; otherwise take first two numeric props as dyes
                string dye1Key = numeric.FirstOrDefault(n => n.Equals("Stain1", StringComparison.OrdinalIgnoreCase) || n.Equals("Dye1", StringComparison.OrdinalIgnoreCase))
                                 ?? numeric.ElementAtOrDefault(0)
                                 ?? "Stain1";
                string dye2Key = numeric.FirstOrDefault(n => n.Equals("Stain2", StringComparison.OrdinalIgnoreCase) || n.Equals("Dye2", StringComparison.OrdinalIgnoreCase))
                                 ?? numeric.ElementAtOrDefault(1)
                                 ?? "Stain2";

                return ((JObject)root.DeepClone(), (JObject)((JObject)root.DeepClone())[prop.Name]!, prop.Name, slotProp.Name, itemKey, dye1Key, dye2Key);
            }
        }

        // No container with enum-named slots -> create a new one named "Equipment" with a minimal schema.
        var equip = new JObject();
        root["Equipment"] = equip;
        return ((JObject)root.DeepClone(), equip, "Equipment", nameof(EquipSlot.Body), "ItemId", "Stain1", "Stain2");
    }
    private static string SlotKeyFor(ResolvedRow r)
    {
        // Prefer enum names (PascalCase) which match ApiEquipSlot
        return Enum.TryParse<EquipSlot>(r.Slot, true, out var es) ? es.ToString() : r.Slot;
    }

    public static void ApplySmart(IReadOnlyList<ResolvedRow> rows)
    {
        var idx = GetLocalObjectIndex();
        if (idx < 0) { Plugin.Chat("No local player."); return; }

        try
        {
            var (state, equip, equipKey, schemaSlotKey, itemKey, dye1Key, dye2Key) = GetStateAndSchema(idx);

            // Ensure toggles present
            state["ApplyGear"] = state["ApplyGear"] ?? true;
            state["ApplyDyes"] = state["ApplyDyes"] ?? true;

            // Make sure equip refers to the clone inside 'state'
            equip = (JObject)state[equipKey]!;

            foreach (var r in rows)
            {
                // Map "Ring" -> both rings explicitly
                if (r.Slot.Equals("Ring", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var ring in new[] { "LFinger", "RFinger", "LeftRing", "RightRing" }) // cover both enum and legacy names
                    {
                        var slotKey = equip[ring] is JObject ? ring : null;
                        if (slotKey == null) continue;
                        var node = (JObject)equip[slotKey]!;
                        node[itemKey] = (uint)r.ItemId;
                        node[dye1Key] = r.Stain1Id ?? 0u;
                        node[dye2Key] = r.Stain2Id ?? 0u;
                    }
                    continue;
                }

                var key = SlotKeyFor(r);

                // If this slot exists, patch its known keys.
                if (equip[key] is JObject nodeExisting)
                {
                    nodeExisting[itemKey] = (uint)r.ItemId;
                    nodeExisting[dye1Key] = r.Stain1Id ?? 0u;
                    nodeExisting[dye2Key] = r.Stain2Id ?? 0u;
                    continue;
                }

                // If not, clone the schema from the exemplar slot node to preserve any extra fields.
                if (equip[schemaSlotKey] is JObject exemplar)
                {
                    var clone = (JObject)exemplar.DeepClone();
                    // overwrite the three important fields
                    clone[itemKey] = (uint)r.ItemId;
                    clone[dye1Key] = r.Stain1Id ?? 0u;
                    clone[dye2Key] = r.Stain2Id ?? 0u;
                    equip[key] = clone;
                }
                else
                {
                    // Absolute fallback: create minimal node with the known key names.
                    var node = new JObject
                    {
                        [itemKey] = (uint)r.ItemId,
                        [dye1Key] = r.Stain1Id ?? 0u,
                        [dye2Key] = r.Stain2Id ?? 0u,
                    };
                    equip[key] = node;
                }
            }

            // Debug: one-line sample so we can verify keys
            try
            {
                var prop = ((JObject)state[equipKey]!).Properties().FirstOrDefault();
                var sample = prop is null ? "{}" : new JObject { [prop.Name] = prop.Value }.ToString(Newtonsoft.Json.Formatting.None);
                Plugin.Log.Info($"[EorzeaLink] equipKey={equipKey} itemKey={itemKey} dyeKeys=({dye1Key},{dye2Key}) sample={sample}");
            }
            catch { }

            // === CLEAR any slot not specified by EC ===
            var desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // build desired set from EC rows (cover enum names + legacy ring names)
            foreach (var r in rows)
            {
                if (r.Slot.Equals("Ring", StringComparison.OrdinalIgnoreCase))
                {
                    desired.Add("RFinger");
                    desired.Add("LFinger");
                    desired.Add("RightRing");
                    desired.Add("LeftRing");
                    continue;
                }

                var enumName = SlotMap.TryGetValue(r.Slot, out var es) ? es.ToString() : r.Slot;
                desired.Add(enumName);
                desired.Add(r.Slot);
            }

            
            // walk existing equipment entries; clear anything not desired
            foreach (var prop in equip.Properties().ToList())
            {
                if (prop.Value is not JObject node) continue;

                var name = prop.Name;

                // Never clear MainHand
                if (name.Equals("MainHand", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                if (name.Equals("OffHand", StringComparison.OrdinalIgnoreCase) && !desired.Contains("OffHand"))
                    continue;

                // treat ring aliases equivalently
                bool isDesired =
                    desired.Contains(name) ||
                    (name.Equals("RFinger", StringComparison.OrdinalIgnoreCase)  && (desired.Contains("RFinger")  || desired.Contains("RightRing"))) ||
                    (name.Equals("LFinger", StringComparison.OrdinalIgnoreCase)  && (desired.Contains("LFinger")  || desired.Contains("LeftRing")))  ||
                    (name.Equals("RightRing", StringComparison.OrdinalIgnoreCase)&& (desired.Contains("RightRing")|| desired.Contains("RFinger")))   ||
                    (name.Equals("LeftRing", StringComparison.OrdinalIgnoreCase) && (desired.Contains("LeftRing") || desired.Contains("LFinger")));

                if (!isDesired)
                    ClearSlotNode(node, itemKey, dye1Key, dye2Key);
            }

            var apply = new ApplyState(Plugin.Pi);
            var flags = ApplyFlagEx.DesignDefault;
            var rc = apply.Invoke(state, idx, 0u, flags);

            if (rc < 0)
            {
                var json = state.ToString(Newtonsoft.Json.Formatting.None);
                var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                rc = apply.Invoke(b64, idx, 0u, flags);
            }

            Plugin.Chat(rc >= 0 ? "Applied via Glamourer." : $"Glamourer error {rc}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "EorzeaLink.ApplySmart");
            Plugin.Chat($"Apply failed: {ex.Message}");
        }
    }
}