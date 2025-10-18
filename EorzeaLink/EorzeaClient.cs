using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace EorzeaLink;

public record ParsedRow(string SlotHint, string ItemName, string? Dye1, string? Dye2 = null);
public sealed record ParsedResult(string? Title, string? Author, List<ParsedRow> Rows);
public static class EorzeaClient
{
    public static async Task<ParsedResult> ParseAsync(
    HttpClient http, string url, CancellationToken ct = default,
    string proxyUrl = "", string? proxyKey = null)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            return new ParsedResult(null, null, new()); // hard-require the proxy

        var baseUrl = proxyUrl.TrimEnd('/');
        await EnsureClientCredsAsync(http, proxyUrl, ct);

        // function to call /parse
        async Task<HttpResponseMessage> DoParseAsync()
        {
            var reqUrl = $"{baseUrl}/parse?url={Uri.EscapeDataString(url)}";
            var rq = new HttpRequestMessage(HttpMethod.Get, reqUrl);
            if (!string.IsNullOrWhiteSpace(Plugin.Cfg.ClientId) &&
                !string.IsNullOrWhiteSpace(Plugin.Cfg.ClientSecret))
                Sign(rq, Plugin.Cfg.ClientId!, Plugin.Cfg.ClientSecret!, url);
            return await http.SendAsync(rq, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        // first attempt
        using var resp1 = await DoParseAsync();

        if ((int)resp1.StatusCode == 401 || (int)resp1.StatusCode == 403)
        {
            Plugin.Chat("Proxy auth expired; re-registering…");

            // Only retry if this looks like "unknown_client"/lost registry.
            // Optional: check body text before retrying to avoid loops.
            // Re-register → save → retry once.
            try
            {
                await EnsureClientCredsAsync(http, baseUrl, ct, force: true);
                using var resp2 = await DoParseAsync();
                if (!resp2.IsSuccessStatusCode)
                    return new ParsedResult(null, null, new());
                var body2 = await resp2.Content.ReadAsStringAsync(ct);
                return ParseFromJson(body2);
            }
            catch
            {
                return new ParsedResult(null, null, new());
            }
        }

        if (!resp1.IsSuccessStatusCode)
            return new ParsedResult(null, null, new());

        var json = await resp1.Content.ReadAsStringAsync(ct);
        return ParseFromJson(json);
    }

    private static ParsedResult ParseFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? title = null;
        string? author = null;
        if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
            title = t.GetString();

        if (root.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.String)
            author = a.GetString();

        var rowsProp = root.GetProperty("rows");
        var rows = new List<ParsedRow>(rowsProp.GetArrayLength());
        foreach (var r in rowsProp.EnumerateArray())
        {
            var slot = r.GetProperty("slot").GetString() ?? "";
            var item = r.GetProperty("item").GetString() ?? "";
            string? dye1 = null, dye2 = null;
            if (r.TryGetProperty("dyes", out var d) && d.ValueKind == JsonValueKind.Array)
            {
                int idx = 0;
                foreach (var x in d.EnumerateArray())
                {
                    var val = (x.ValueKind == JsonValueKind.String ? x.GetString() : null)?.Trim();
                    if (string.IsNullOrEmpty(val)) val = null; // treat "Undyed"/"" upstream as null if you want
                    if (idx == 0) dye1 = val;
                    else if (idx == 1) { dye2 = val; break; }
                    idx++;
                }
            }
            else if (r.TryGetProperty("dye", out var d1) && d1.ValueKind == JsonValueKind.String)
            {
                // legacy single-dye shape
                var val = d1.GetString()?.Trim();
                dye1 = string.IsNullOrEmpty(val) ? null : val;
            }

            rows.Add(new ParsedRow(slot, item, dye1, dye2));
        }

        return new ParsedResult(title, author, rows);
    }

    private static async Task EnsureClientCredsAsync(HttpClient http, string baseUrl, CancellationToken ct, bool force = false)
    {
        if (!force &&
            !string.IsNullOrWhiteSpace(Plugin.Cfg.ClientId) &&
            !string.IsNullOrWhiteSpace(Plugin.Cfg.ClientSecret))
            return;

        var url = baseUrl.TrimEnd('/') + "/register";
        using var rq = new HttpRequestMessage(HttpMethod.Post, url)
        { Content = new StringContent("{}", Encoding.UTF8, "application/json") };

        using var resp = await http.SendAsync(rq, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        Plugin.Cfg.ClientId = doc.RootElement.GetProperty("clientId").GetString();
        Plugin.Cfg.ClientSecret = doc.RootElement.GetProperty("secret").GetString();
        Plugin.Save();
    }

    private static void Sign(HttpRequestMessage rq, string clientId, string secret, string url)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var msg = $"{url}|{ts}";
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(msg))).ToLowerInvariant();

        rq.Headers.TryAddWithoutValidation("x-client", clientId);
        rq.Headers.TryAddWithoutValidation("x-ts", ts);
        rq.Headers.TryAddWithoutValidation("x-sig", sig);
    }
}
