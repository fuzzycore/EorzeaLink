using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace EorzeaLink;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "EorzeaLink";
    internal static PluginConfig Cfg { get; private set; } = null!;
    internal static void Save() { try { Pi.SavePluginConfig(Cfg); } catch { } }

    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Cmd { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    internal static AllaganToolsBridge? AtBridge;

    private readonly WindowSystem _ws = new("EorzeaLink");
    private readonly MainWindow _win;
    private List<ResolvedRow> _lastResolved = new();

    private void DrawUI() => _ws.Draw();
    private void OpenWin() => _win.IsOpen = true;


    private static readonly HttpClient _http = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
        UseCookies = false
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public Plugin()
    {
        Cfg = Pi.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        Save();

        try { AtBridge = new AllaganToolsBridge(Pi); }
        catch { AtBridge = null; }

        _win = new MainWindow(url => ElinkPreviewAsync(url));
        _ws.AddWindow(_win);
        Pi.UiBuilder.Draw += DrawUI;
        Pi.UiBuilder.OpenMainUi += OpenWin;
        Pi.UiBuilder.OpenConfigUi += OpenWin;

        // Commands
        Cmd.AddHandler("/elink", new CommandInfo(OnElink)
        {
            HelpMessage = "Open EorzeaLink."
        });
        Cmd.AddHandler("/elink <url>", new CommandInfo(OnElink)
        {
            HelpMessage = "Preview an EorzeaCollection glam by URL."
        });

        // Cmd.AddHandler("/ecpreview", new CommandInfo(OnEcPreview) { HelpMessage = "Preview an EorzeaCollection glam by URL." });
        // Cmd.AddHandler("/ecapply", new CommandInfo(OnEcApply) { HelpMessage = "Parse + open preview (apply soon)." });

        // Default headers (chrome-y)
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
    "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
    }

    private void OnElink(string cmd, string args)
    {
        var arg = (args ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(arg))
        {
            _win.IsOpen = true;
            Chat("Opening EorzeaLink.");
            return;
        }

        _win.BeginLoading(arg);
        _ = Task.Run(() => ElinkPreviewAsync(arg));
    }

    private async Task ElinkPreviewAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var parsed = await EorzeaClient.ParseAsync(_http, url, ct, proxyUrl: Plugin.Cfg.WorkerUrl ?? "");
            if (parsed.Rows.Count == 0) {
                var error = "No items found on that page.";

                _win.SetError(error);
                Chat(error);
                return;
            }

            var resolved = Resolver.ResolveAll(Data, parsed.Rows);
            Ownership.Annotate(resolved);
            _lastResolved = resolved;

            _win.SetPreview(resolved, url, parsed.Title, parsed.Author);
            _win.IsOpen = true;

            Chat($"Parsed {resolved.Count} items.");
        }
        catch (HttpRequestException ex) {
            _win.SetError($"Fetch failed: {ex.Message}");
            Chat($"Fetch failed: {ex.Message}");
        }
        catch (Exception ex) {
            _win.SetError("Something went wrong with parsing. Did EorzeaCollection change?");
            Log.Error(ex, "elink");
            Chat("Parse failed — see /xllog.");
        }
    }

    // dispose
    public void Dispose()
    {
        _ws.RemoveAllWindows();
        Cmd.RemoveHandler("/elink");
        Pi.UiBuilder.Draw -= DrawUI;
        Pi.UiBuilder.OpenMainUi -= OpenWin;
        Pi.UiBuilder.OpenConfigUi -= OpenWin;
        _http.Dispose();
    }

    internal static void Chat(string msg)
    {
        try { ChatGui.Print($"[EorzeaLink] {msg}"); } catch { }
    }
}
