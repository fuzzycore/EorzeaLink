using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using static EorzeaLink.Ownership;
using System.Numerics;

namespace EorzeaLink;

public sealed class MainWindow : Window
{
    private readonly Func<string, Task> _onPreview;
    private List<ResolvedRow> _rows = new();
    // public IReadOnlyList<ResolvedRow> Rows => _rows;
    private string _sourceUrl = "";
    private string _title = "";
    private string _author = "";
    private bool _loading = false;
    private string _status = string.Empty;

    public MainWindow() : this(_ => Task.CompletedTask) { }

    public MainWindow(Func<string, Task> onPreview) : base("EorzeaLink", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _onPreview = onPreview ?? (_ => Task.CompletedTask);
    }

    public void BeginLoading(string url, string status = "Fetching glamour. Please wait.")
    {
        _sourceUrl = url ?? string.Empty;
        _title = string.Empty;
        _author = string.Empty;
        _rows.Clear();
        _status = status;
        _loading = true;
        IsOpen = true;
    }

    public void SetPreview(IReadOnlyList<ResolvedRow> rows, string url, string? title, string? author)
    {
        _rows.Clear();
        _rows.AddRange(rows ?? Array.Empty<ResolvedRow>());
        _sourceUrl = url;
        _title = title ?? string.Empty;
        _author = author ?? string.Empty;
        _status = string.Empty;
        _loading = false;
    }

    public void SetError(string message)
    {
        _rows.Clear();
        _title = string.Empty;
        _author = string.Empty;
        _status = message;
        _loading = false;
    }


    public override void Draw()
    {
        ImGui.TextUnformatted("EorzeaCollection URL");
        ImGui.PushItemWidth(520);
        bool submitted = ImGui.InputTextWithHint(
            "##elink-url",
            "https://ffxiv.eorzeacollection.com/glamour/...",
            ref _sourceUrl,
            512,
            ImGuiInputTextFlags.EnterReturnsTrue
        );
        ImGui.PopItemWidth();
        ImGui.SameLine();

        bool canClick = !string.IsNullOrWhiteSpace(_sourceUrl);
        if (!canClick) ImGui.BeginDisabled();
        if (ImGui.Button("Preview") || submitted)
        {
            var url = _sourceUrl.Trim();
            if (url.Length > 0)
            {
                BeginLoading(url);
                _ = _onPreview(url);
            }
        }
        if (!canClick) ImGui.EndDisabled();

        ImGui.Separator();

        if (_loading)
        {
            ImGui.TextDisabled(string.IsNullOrEmpty(_status) ? "Please wait…" : _status);
            return; // Don't render below just yet
        }

        // if (!string.IsNullOrEmpty(_sourceUrl))
        if (!string.IsNullOrWhiteSpace(_title))
            ImGui.TextUnformatted(_title);

        if (!string.IsNullOrWhiteSpace(_author))
            ImGui.TextUnformatted($"by {_author}");

        if (Plugin.AtBridge?.Ready != true)
        {
            ImGui.TextWrapped(
                "Note: Allagan Tools plugin not detected. Ownership info may be incomplete. " +
                "For best results, install Allagan Tools from Dalamud's plugin repository."
            );
        }

        if (ImGui.BeginTable("preview", 6,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Own");
            ImGui.TableSetupColumn("Slot");
            ImGui.TableSetupColumn("Item Name");
            ImGui.TableSetupColumn("ItemId");
            ImGui.TableSetupColumn("Dye1Id");
            ImGui.TableSetupColumn("Dye2Id");
            ImGui.TableHeadersRow();

            foreach (var r in _rows)
            {
                ImGui.TableNextRow();

                // Own (colored glyph + tooltip)
                ImGui.TableSetColumnIndex(0);
                ImGui.PushStyleColor(ImGuiCol.Text, OwnColor(r.Own));
                ImGui.TextUnformatted(OwnGlyph(r.Own));
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Ownership: {r.Own} (via {r.OwnSource})");

                // Slot
                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(r.Slot);

                // Item
                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(r.ItemName);

                // ItemId
                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(r.ItemId.ToString());

                // Dye1
                ImGui.TableSetColumnIndex(4);
                ImGui.TextUnformatted(r.Stain1Id?.ToString() ?? "-");

                // Dye2
                ImGui.TableSetColumnIndex(5);
                ImGui.TextUnformatted(r.Stain2Id?.ToString() ?? "-");
            }

            ImGui.EndTable();
        }

        ImGui.Separator();

        if (_rows.Count > 0)
        {
            if (ImGui.Button("Apply via Glamourer"))
                GlamourerBridge.ApplySmart(_rows);

            ImGui.SameLine();
            ImGui.TextUnformatted($"{_rows.Count} items parsed");
        }
        else
        {
            ImGui.TextDisabled("No items parsed yet.");
        }
    }

    private static Vector4 OwnColor(OwnStatus s) => s switch
    {
        OwnStatus.Have     => new(0.55f, 0.95f, 0.55f, 1f),  // green
        OwnStatus.Unknown  => new(0.95f, 0.85f, 0.45f, 1f),  // yellow
        _ /* NotHave */    => new(0.85f, 0.40f, 0.40f, 1f),  // red
    };

    private static string OwnGlyph(OwnStatus s) => s switch
    {
        OwnStatus.Have    => "✓",
        OwnStatus.Unknown => "?",
        _                 => "×",
    };
}
