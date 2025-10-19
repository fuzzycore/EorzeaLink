using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace EorzeaLink;


internal sealed class AllaganToolsBridge
{
    private readonly ICallGateSubscriber<uint, bool, uint[], uint>? _itemCountOwned;
    private readonly ICallGateSubscriber<bool>? _isInitialized;

    public AllaganToolsBridge(IDalamudPluginInterface pi)
    {
        try { _isInitialized   = pi.GetIpcSubscriber<bool>("AllaganTools.IsInitialized"); } catch { }
        try { _itemCountOwned  = pi.GetIpcSubscriber<uint, bool, uint[], uint>("AllaganTools.ItemCountOwned"); } catch { }
    }

    public bool Ready { get { try { return _isInitialized?.InvokeFunc() ?? false; } catch { return false; } } }
    public bool Available => _itemCountOwned is not null;

    public bool TryCountOwned(uint itemId, out uint count)
    {
        count = 0;
        if (_itemCountOwned is null || !Ready) return false;
        // itemId, currentCharacterOnly, invTypes
        try { count = _itemCountOwned.InvokeFunc(itemId, true, FullInvTypes()); return true; }
        catch { return false; }
    }

    static uint[] FullInvTypes() => new uint[]
    {
        // Bags
        0, 1, 2, 3,
        // Armory
        3200, 3201, 3202, 3203, 3204, 3205, 3206, 3207, 3208, 3209, 3300,
        // Saddlebag
        4000, 4001, 4100, 4101,
        // Retainers (pages 1..7 + equipped)
        10000,10001,10002,10003,10004,10005,10006,11000,
        // Dresser / Armoire
        2501, // Glamour Dresser
        2500, // Armoire
    };
}
