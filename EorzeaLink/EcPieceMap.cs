using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace EorzeaLink;

internal static class EcPieceMap
{
    private static Dictionary<int, int>? _map;
    private static bool _initialized;

    public static int Count => EnsureLoaded().Count;

    public static bool TryGet(int xivApiId, out int ecPieceId) =>
        EnsureLoaded().TryGetValue(xivApiId, out ecPieceId);

    public static void Initialize(string? assemblyDirectory)
    {
        if (_initialized)
            return;

        _map = Load(assemblyDirectory);
        _initialized = true;
        Plugin.Log.Info("EcPieceMap: loaded {Count} entries", _map.Count);
    }

    private static Dictionary<int, int> EnsureLoaded()
    {
        if (_initialized && _map is not null)
            return _map;

        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _map = Load(dir);
        _initialized = true;
        return _map;
    }

    private static Dictionary<int, int> Load(string? assemblyDirectory)
    {
        foreach (var json in EnumerateSources(assemblyDirectory))
        {
            try
            {
                var map = Parse(json);
                if (map.Count > 0)
                    return map;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "EcPieceMap: failed parsing candidate source");
            }
        }

        Plugin.Log.Error("EcPieceMap: no piece map could be loaded");
        return new Dictionary<int, int>();
    }

    private static IEnumerable<string> EnumerateSources(string? assemblyDirectory)
    {
        var asm = Assembly.GetExecutingAssembly();
        var embeddedName = $"{asm.GetName().Name}.Data.ec-piece-map.json";
        using (var stream = asm.GetManifestResourceStream(embeddedName))
        {
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                yield return reader.ReadToEnd();
            }
        }

        if (string.IsNullOrWhiteSpace(assemblyDirectory))
            yield break;

        foreach (var fileName in new[] { "ec-piece-map.json", Path.Combine("Data", "ec-piece-map.json") })
        {
            var path = Path.Combine(assemblyDirectory, fileName);
            if (File.Exists(path))
                yield return File.ReadAllText(path);
        }
    }

    private static Dictionary<int, int> Parse(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
        var map = new Dictionary<int, int>(raw.Count);
        foreach (var (key, value) in raw)
        {
            if (!int.TryParse(key, out var xiv) || xiv <= 0 || value <= 0)
                continue;
            map[xiv] = value;
        }

        return map;
    }
}
