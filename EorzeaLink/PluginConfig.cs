using Dalamud.Configuration;
using Dalamud.Plugin;

namespace EorzeaLink;

public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public string? WorkerUrl { get; set; } = "https://eorzealink-proxy.onrender.com";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
