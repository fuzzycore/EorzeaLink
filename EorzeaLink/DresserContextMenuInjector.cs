using System;
using System.Runtime.InteropServices;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EorzeaLink;

internal sealed unsafe class DresserContextMenuInjector : IDisposable
{
    private const int HeaderCount = 8;

    private readonly IDataManager _data;
    private readonly IFramework _framework;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IAddonLifecycle.AddonEventDelegate _preSetupHandler;
    private readonly Hook<OnMenuSelectedDelegate> _menuSelectedHook;

    // Index of our injected item (= native item count before we append one).
    private int _injectedItemIndex = -1;
    private int _pendingXivApiId;

    private delegate bool OnMenuSelectedDelegate(AddonContextMenu* addon, int selectedIdx, byte a3);

    public DresserContextMenuInjector(
        IDataManager data,
        IFramework framework,
        IAddonLifecycle addonLifecycle,
        IGameInteropProvider interop)
    {
        _data = data;
        _framework = framework;
        _addonLifecycle = addonLifecycle;
        _preSetupHandler = OnContextMenuPreSetup;
        _addonLifecycle.RegisterListener(AddonEvent.PreSetup, "ContextMenu", _preSetupHandler);

        _menuSelectedHook = interop.HookFromAddress<OnMenuSelectedDelegate>(
            (nint)AddonContextMenu.StaticVirtualTablePointer->OnMenuSelected,
            OnMenuSelectedDetour);
        _menuSelectedHook.Enable();
    }

    private void OnContextMenuPreSetup(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonSetupArgs setup)
            return;

        var addon = (AtkUnitBase*)setup.Addon.Address;
        if (addon == null)
            return;

        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null)
            return;

        var parent = manager->GetAddonById(addon->BlockedParentId);
        if (!IsDresserContext(parent))
        {
            _injectedItemIndex = -1;
            _pendingXivApiId = 0;
            return;
        }

        if (!ItemContextMenu.TryGetDresserXivApiId(out var xivApiId))
            return;

        var valueCount = (int)setup.AtkValueCount;
        var values = (AtkValue*)setup.AtkValues;
        if (values == null || valueCount < HeaderCount)
            return;

        var nativeCount = (int)values[0].UInt;
        if (nativeCount <= 0 || nativeCount >= 31)
            return;

        var hasDisabled = valueCount > HeaderCount + nativeCount;
        var newItemCount = nativeCount + 1;
        var requiredCount = HeaderCount + (hasDisabled ? newItemCount * 2 : newItemCount);

        values = EnsureCapacity(values, valueCount, requiredCount);
        setup.AtkValues = (nint)values;
        setup.AtkValueCount = (uint)requiredCount;

        _injectedItemIndex = nativeCount;
        _pendingXivApiId = xivApiId;

        values[HeaderCount + nativeCount].SetManagedString(GetMenuLabel().EncodeWithNullTerminator());

        if (hasDisabled)
        {
            for (var i = nativeCount - 1; i >= 0; i--)
                values[HeaderCount + newItemCount + i] = values[HeaderCount + nativeCount + i];

            values[HeaderCount + newItemCount + nativeCount].SetInt(0);
        }

        values[0].SetUInt((uint)newItemCount);
    }

    private bool OnMenuSelectedDetour(AddonContextMenu* addon, int selectedIdx, byte a3)
    {
        if (_injectedItemIndex >= 0 && selectedIdx >= _injectedItemIndex)
        {
            var itemId = _pendingXivApiId;
            _injectedItemIndex = -1;
            _pendingXivApiId = 0;

            _framework.Run(() =>
            {
                ItemContextMenu.HandleDresserClick(_data, itemId);

                var menu = RaptureAtkUnitManager.Instance()->GetAddonByName("ContextMenu");
                if (menu != null && menu->IsVisible)
                    menu->FireCallbackInt(-2);
            });

            return false;
        }

        return _menuSelectedHook.Original(addon, selectedIdx, a3);
    }

    private static unsafe bool IsDresserContext(AtkUnitBase* parent)
    {
        if (parent != null && parent->NameString.ToString().Contains("MiragePrism", StringComparison.Ordinal))
            return true;

        var dresser = AgentMiragePrismPrismBox.Instance();
        return dresser != null && dresser->IsAddonShown();
    }

    private static SeString GetMenuLabel() =>
        new SeStringBuilder()
            .AddUiForeground($"{SeIconChar.BoxedLetterD.ToIconString()} ", 539)
            .Append(new SeString(new TextPayload("See This On EorzeaCollection")))
            .Build();

    private static AtkValue* EnsureCapacity(AtkValue* values, int currentCount, int requiredCount)
    {
        if (currentCount >= requiredCount)
            return values;

        var byteSize = (sizeof(AtkValue) * requiredCount) + 8;
        var allocation = (nint)IMemorySpace.GetUISpace()->Malloc((ulong)byteSize, 0);
        if (allocation == 0)
            return values;

        NativeMemory.Fill((void*)allocation, (nuint)byteSize, 0);
        *(ulong*)allocation = (ulong)requiredCount;

        var newValues = (AtkValue*)(allocation + 8);
        new ReadOnlySpan<AtkValue>(values, currentCount).CopyTo(new Span<AtkValue>(newValues, requiredCount));
        return newValues;
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PreSetup, "ContextMenu", _preSetupHandler);
        _menuSelectedHook.Dispose();
    }
}
