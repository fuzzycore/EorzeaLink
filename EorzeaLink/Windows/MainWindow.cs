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
    private readonly Action<GlamHistoryEntry> _onRestoreHistory;
    private List<ResolvedRow> _rows = new();
    // public IReadOnlyList<ResolvedRow> Rows => _rows;
    private string _sourceUrl = "";
    private string _title = "";
    private string _author = "";
    private bool _loading = false;
    private string _status = string.Empty;

    public MainWindow() : this(_ => Task.CompletedTask, _ => { }) { }

    public MainWindow(Func<string, Task> onPreview, Action<GlamHistoryEntry> onRestoreHistory)
        : base("EorzeaLink")
    {
        _onPreview = onPreview ?? (_ => Task.CompletedTask);
        _onRestoreHistory = onRestoreHistory ?? (_ => { });
        Size = new Vector2(880, 520);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 360),
            MaximumSize = new Vector2(2000, 2000),
        };
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
        var avail = ImGui.GetContentRegionAvail();
        var history = GlamHistory.Entries;
        const float sidebarWidth = 220f;

        if (history.Count > 0)
        {
            ImGui.BeginChild("##history-sidebar", new Vector2(sidebarWidth, avail.Y), true);
            DrawHistorySidebar(history);
            ImGui.EndChild();
            ImGui.SameLine();
        }

        ImGui.BeginChild("##main-content", ImGui.GetContentRegionAvail(), false);
        DrawMainPanel();
        ImGui.EndChild();
    }

    private void DrawMainPanel()
    {
        ImGui.TextUnformatted("EorzeaCollection URL");
        ImGui.PushItemWidth(-100);
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
            return;
        }

        if (!string.IsNullOrWhiteSpace(_status))
        {
            ImGui.TextWrapped(_status);
            return;
        }

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

        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y + 1f;
        ImGui.BeginChild("##preview-scroll", new Vector2(0, -footerHeight), false);
        DrawPreviewTable();
        ImGui.EndChild();

        ImGui.Separator();
        DrawApplyFooter();
    }

    private void DrawPreviewTable()
    {
        var tableSize = ImGui.GetContentRegionAvail();
        if (tableSize.Y < 1f)
            return;

        if (ImGui.BeginTable("preview", 6,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY,
            tableSize))
        {
            ImGui.TableSetupColumn("Own", ImGuiTableColumnFlags.WidthFixed, 28);
            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 72);
            ImGui.TableSetupColumn("Item Name");
            ImGui.TableSetupColumn("ItemId", ImGuiTableColumnFlags.WidthFixed, 56);
            ImGui.TableSetupColumn("Dye1Id", ImGuiTableColumnFlags.WidthFixed, 48);
            ImGui.TableSetupColumn("Dye2Id", ImGuiTableColumnFlags.WidthFixed, 48);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            foreach (var r in _rows)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.PushStyleColor(ImGuiCol.Text, OwnColor(r.Own));
                ImGui.TextUnformatted(OwnGlyph(r.Own));
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Ownership: {r.Own} (via {r.OwnSource})");

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(r.Slot);

                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(r.ItemName);

                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(r.ItemId.ToString());

                ImGui.TableSetColumnIndex(4);
                ImGui.TextUnformatted(r.Stain1Id?.ToString() ?? "-");

                ImGui.TableSetColumnIndex(5);
                ImGui.TextUnformatted(r.Stain2Id?.ToString() ?? "-");
            }

            ImGui.EndTable();
        }
    }

    private void DrawApplyFooter()
    {
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

    private void DrawHistorySidebar(IReadOnlyList<GlamHistoryEntry> history)
    {
        ImGui.TextUnformatted("Recent");
        ImGui.Separator();

        var wrapPos = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        foreach (var entry in history)
        {
            var selected = string.Equals(_sourceUrl, entry.Url, StringComparison.OrdinalIgnoreCase);
            ImGui.PushID(entry.Url);

            var start = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;

            ImGui.PushTextWrapPos(wrapPos);
            ImGui.TextUnformatted(FormatHistoryTitle(entry));
            ImGui.TextDisabled(FormatHistorySubtitle(entry));
            ImGui.PopTextWrapPos();

            var end = ImGui.GetCursorScreenPos();
            var height = end.Y - start.Y;

            ImGui.SetCursorScreenPos(start);
            if (ImGui.Selectable("##entry", selected, ImGuiSelectableFlags.None, new Vector2(width, height)))
            {
                _sourceUrl = entry.Url;
                _onRestoreHistory(entry);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{entry.Url}\n{entry.Rows.Count} items");

            ImGui.SetCursorScreenPos(end);
            ImGui.PopID();
        }
    }
    private static string FormatHistoryTitle(GlamHistoryEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Title)
            ? Truncate(entry.Url, 32)
            : Truncate(entry.Title.Trim(), 32);

    private static string FormatHistorySubtitle(GlamHistoryEntry entry)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(entry.Author))
            parts.Add(entry.Author.Trim());
        parts.Add(FormatRetrievedAt(entry.RetrievedAt));
        return string.Join(" · ", parts);
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
            return text;
        return text[..(max - 1)] + "…";
    }

    private static string FormatRetrievedAt(DateTime utc)
    {
        var elapsed = DateTime.UtcNow - utc;
        if (elapsed.TotalMinutes < 1) return "just now";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalDays < 1) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
        return utc.ToLocalTime().ToString("g");
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
