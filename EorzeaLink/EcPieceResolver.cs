using System.Threading;
using System.Threading.Tasks;

namespace EorzeaLink;

internal static class EcPieceResolver
{
    public static Task<int?> ResolveEcPieceIdAsync(int xivApiId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (xivApiId <= 0)
            return Task.FromResult<int?>(null);

        if (Plugin.Cfg.EcPieceCache.TryGetValue(xivApiId, out var cached))
            return Task.FromResult<int?>(cached);

        if (EcPieceMap.TryGet(xivApiId, out var ecPieceId))
            return Task.FromResult<int?>(Remember(xivApiId, ecPieceId));

        return Task.FromResult<int?>(null);
    }

    private static int Remember(int xivApiId, int ecPieceId)
    {
        Plugin.Cfg.EcPieceCache[xivApiId] = ecPieceId;
        Plugin.Save();
        return ecPieceId;
    }
}
