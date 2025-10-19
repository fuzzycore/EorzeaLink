// using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game; // InventoryManager, InventoryType

namespace EorzeaLink;

public static class Ownership
{
    public enum OwnStatus { Unknown, Have, NotHave }  // <-- make this public
    public readonly record struct OwnResult(uint ItemId, OwnStatus Status, string Source);

    public static unsafe OwnResult Check(uint itemId)
    {
        if (Plugin.AtBridge is { Available: true } at)
        {
            if (at.TryCountOwned(itemId, out var owned))
                return new(itemId, owned > 0 ? OwnStatus.Have : OwnStatus.NotHave, "AllaganTools");
        }
    
        // 1) Main inventory (4 bags)
        foreach (var t in new[]
        {
            InventoryType.Inventory1, InventoryType.Inventory2,
            InventoryType.Inventory3, InventoryType.Inventory4,
        })
        {
            if (FindInContainer(itemId, t))
                return new(itemId, OwnStatus.Have, "Inventory");
        }

        // 2) Armoury chest / Equipped
        foreach (var t in new[]
        {
            InventoryType.ArmoryMainHand,
            InventoryType.ArmoryOffHand,
            InventoryType.ArmoryHead,
            InventoryType.ArmoryBody,
            InventoryType.ArmoryHands,
            InventoryType.ArmoryLegs,
            InventoryType.ArmoryFeets,       // note: Feets
            InventoryType.ArmoryEar,         // note: Ear
            InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist,
            InventoryType.ArmoryRings,       // both rings bucket
            InventoryType.EquippedItems,
        })
        {
            if (FindInContainer(itemId, t))
                return new(itemId, OwnStatus.Have, $"Armoury:{t}");
        }

        // 3) Saddlebags
        foreach (var t in new[]
        {
            InventoryType.SaddleBag1, InventoryType.SaddleBag2,
            InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2
        })
        {
            if (FindInContainer(itemId, t))
                return new(itemId, OwnStatus.Have, $"Saddlebags:{t}");
        }

        // (Optional later: SaddleBags/Retainers when those UIs are open)

        return new(itemId, OwnStatus.NotHave, "—");
    }

    public static void Annotate(IReadOnlyList<ResolvedRow> rows)
    {
        foreach (var r in rows)
        {
            var own = Check((uint)r.ItemId);
            r.Own = own.Status;
            r.OwnSource = own.Source;
        }
    }

    private static unsafe bool FindInContainer(uint itemId, InventoryType type)
    {
        var mgr = InventoryManager.Instance();
        if (mgr == null) return false;

        var cont = mgr->GetInventoryContainer(type);
        if (cont == null) return false;

        for (int i = 0; i < cont->Size; i++)
        {
            var slot = cont->GetInventorySlot(i);
            if (slot == null) continue;
            if (slot->ItemId == itemId && slot->Quantity > 0)
                return true;
        }
        return false;
    }
}