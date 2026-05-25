using System;
using System.Threading.Tasks;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace EorzeaLink;

internal sealed class ItemContextMenu : IDisposable
{
    private readonly IContextMenu _contextMenu;
    private readonly IDataManager _data;

    public ItemContextMenu(IContextMenu contextMenu, IDataManager data)
    {
        _contextMenu = contextMenu;
        _data = data;
        _contextMenu.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.MenuType != ContextMenuType.Inventory)
            return;

        if (args.Target is not MenuTargetInventory inventoryTarget)
            return;

        var targetItem = inventoryTarget.TargetItem;
        if (targetItem is not { IsEmpty: false } item)
            return;

        var xivApiId = (int)item.BaseItemId;
        if (xivApiId <= 0)
            return;

        var slot = Resolver.GetEquipSlot(_data, (uint)xivApiId);
        if (slot == "Unknown")
            return;

        args.AddMenuItem(new MenuItem
        {
            Name = new SeString(new TextPayload("See This On EorzeaCollection")),
            UseDefaultPrefix = true,
            OnClicked = clicked =>
            {
                _ = OpenGlamSearchAsync(xivApiId, slot);
            },
        });
    }

    private async Task OpenGlamSearchAsync(int xivApiId, string slot)
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

    public void Dispose() => _contextMenu.OnMenuOpened -= OnMenuOpened;
}
