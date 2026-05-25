using System;
using System.Threading.Tasks;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace EorzeaLink;

internal sealed class ItemContextMenu : IDisposable
{
    private readonly IContextMenu _contextMenu;
    private readonly IDataManager _data;
    private readonly DresserContextMenuInjector _dresserInjector;

    public ItemContextMenu(
        IContextMenu contextMenu,
        IDataManager data,
        IFramework framework,
        IAddonLifecycle addonLifecycle,
        IGameInteropProvider interop)
    {
        _contextMenu = contextMenu;
        _data = data;
        _dresserInjector = new DresserContextMenuInjector(data, framework, addonLifecycle, interop);
        _contextMenu.OnMenuOpened += OnMenuOpened;
    }

    internal static void HandleDresserClick(IDataManager data, int xivApiId = 0)
    {
        if (xivApiId <= 0 && !TryGetDresserXivApiId(out xivApiId))
        {
            Plugin.Chat("Couldn't identify this glamour dresser item.");
            return;
        }

        var slot = Resolver.GetEquipSlot(data, (uint)xivApiId);
        if (slot == "Unknown")
        {
            Plugin.Chat("This item isn't searchable on Eorzea Collection.");
            return;
        }

        _ = OpenGlamSearchAsync(data, xivApiId, slot);
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.MenuType is not (ContextMenuType.Inventory or ContextMenuType.Default))
            return;

        if (args.MenuType == ContextMenuType.Default && !IsGlamourDresserContext(args))
            return;

        if (args.MenuType == ContextMenuType.Inventory)
        {
            var isDresser = IsGlamourDresserContext(args);
            if (!isDresser)
            {
                if (!TryGetXivApiId(args, out var xivApiId))
                    return;

                if (Resolver.GetEquipSlot(_data, (uint)xivApiId) == "Unknown")
                    return;
            }
        }

        args.AddMenuItem(new MenuItem
        {
            Name = new SeString(new TextPayload("See This On EorzeaCollection")),
            UseDefaultPrefix = true,
            OnClicked = clicked =>
            {
                if (IsGlamourDresserContext(clicked))
                {
                    HandleDresserClick(_data);
                    return;
                }

                if (!TryGetXivApiId(clicked, out var clickedItemId))
                    return;

                var slot = Resolver.GetEquipSlot(_data, (uint)clickedItemId);
                if (slot == "Unknown")
                    return;

                _ = OpenGlamSearchAsync(_data, clickedItemId, slot);
            },
        });
    }

    private static unsafe bool IsGlamourDresserContext(IMenuArgs args)
    {
        if (args.AddonName?.Contains("MiragePrism", StringComparison.Ordinal) == true)
            return true;

        var dresserAgent = AgentMiragePrismPrismBox.Instance();
        if (dresserAgent == null)
            return false;

        if (dresserAgent->IsAddonShown())
            return true;

        if (args.MenuType == ContextMenuType.Inventory && args.AgentPtr != 0)
        {
            var context = (AgentInventoryContext*)args.AgentPtr;
            if (context->OwnerAddonId == dresserAgent->AddonId)
                return true;
        }

        return false;
    }

    private static unsafe bool TryGetXivApiId(IMenuArgs args, out int xivApiId)
    {
        xivApiId = 0;

        // Dresser menus expose a non-null but invalid AgentPtr — never dereference it.
        if (IsGlamourDresserContext(args))
            return TryGetDresserXivApiId(out xivApiId);

        if (args.Target is MenuTargetInventory inventoryTarget)
        {
            var targetItem = inventoryTarget.TargetItem;
            if (targetItem is { IsEmpty: false } item)
            {
                xivApiId = (int)item.BaseItemId;
                return xivApiId > 0;
            }
        }

        if (args.AgentPtr == 0)
            return false;

        var context = (AgentInventoryContext*)args.AgentPtr;
        var dummy = context->TargetDummyItem;
        if (!dummy.IsEmpty())
        {
            xivApiId = (int)ItemUtil.GetBaseId(dummy.GetItemId()).ItemId;
            return xivApiId > 0;
        }

        return false;
    }

    internal static unsafe bool TryGetDresserXivApiId(out int xivApiId)
    {
        xivApiId = 0;

        var agent = AgentMiragePrismPrismBox.Instance();
        if (agent == null)
            return false;

        var data = agent->Data;
        if (data == null)
            return false;

        if (data->TempContextItem.ItemId != 0)
        {
            xivApiId = (int)ItemUtil.GetBaseId(data->TempContextItem.ItemId).ItemId;
            if (xivApiId > 0)
                return true;
        }

        var contextIndex = data->TempContextItemIndex;
        if (contextIndex >= 0 && contextIndex < data->PrismBoxItems.Length)
        {
            var boxItem = data->PrismBoxItems[contextIndex];
            if (boxItem.ItemId != 0)
            {
                xivApiId = (int)ItemUtil.GetBaseId(boxItem.ItemId).ItemId;
                if (xivApiId > 0)
                    return true;
            }
        }

        var mirage = MirageManager.Instance();
        if (mirage == null)
            return false;

        var storageIndex = data->TempContextItem.Slot;
        if (storageIndex < mirage->PrismBoxItemIds.Length)
        {
            var itemId = mirage->PrismBoxItemIds[(int)storageIndex];
            if (itemId != 0)
            {
                xivApiId = (int)ItemUtil.GetBaseId(itemId).ItemId;
                if (xivApiId > 0)
                    return true;
            }
        }

        return false;
    }

    private static async Task OpenGlamSearchAsync(IDataManager data, int xivApiId, string slot)
    {
        try
        {
            Plugin.Chat("Looking up item on Eorzea Collection…");

            var ecPieceId = await EcPieceResolver.ResolveEcPieceIdAsync(xivApiId);
            if (ecPieceId is not { } pieceId)
            {
                Plugin.Log.Warning(
                    "EcPiece lookup miss: xivApiId={XivApiId}, mapEntries={Count}",
                    xivApiId,
                    EcPieceMap.Count);
                Plugin.Chat($"No Eorzea Collection match for item {xivApiId} (map has {EcPieceMap.Count} entries).");
                return;
            }

            if (EorzeaCollectionUrls.GlamSearchUrl(pieceId, slot) is not { } url)
            {
                Plugin.Chat("Couldn't build Eorzea Collection link.");
                return;
            }

            Util.OpenLink(url);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Open EorzeaCollection glam search");
            Plugin.Chat("Couldn't open browser.");
        }
    }

    public void Dispose()
    {
        _contextMenu.OnMenuOpened -= OnMenuOpened;
        _dresserInjector.Dispose();
    }
}
