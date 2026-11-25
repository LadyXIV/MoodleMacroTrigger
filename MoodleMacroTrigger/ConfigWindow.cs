using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MoodleMacroTrigger;

public sealed class ConfigWindow : Window
{
    private static readonly string[] IfBusyNames = Enum.GetNames<IfBusy>();

    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Moodle Macro Trigger###MoodleMacroTrigger")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(620, 300), MaximumSize = new Vector2(float.MaxValue) };
    }

    public override void Draw()
    {
        var current = plugin.CurrentMoodles();
        DrawRules(current);
        ImGui.Separator();
        DrawActiveMoodles(current);
        if (plugin.LastFired.Length > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled($"Last: {plugin.LastFired}");
        }
    }

    private void DrawRules(List<Moodle>? current)
    {
        var rules = plugin.Config.Rules;
        var changed = false;
        int? remove = null;

        if (ImGui.BeginTable("rules", 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("On");
            ImGui.TableSetupColumn("Moodle ID", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Applied by", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Macro");
            ImGui.TableSetupColumn("If busy");
            ImGui.TableSetupColumn("");
            ImGui.TableSetupColumn("");
            ImGui.TableHeadersRow();

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                changed |= ImGui.Checkbox("##enabled", ref rule.Enabled);

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                changed |= ImGui.InputText("##moodle", ref rule.MoodleId, 64);
                DrawIdStatus(rule, current);

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                changed |= ImGui.InputTextWithHint("##applier", "Name or Name@World", ref rule.Applier, 64);

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(100);
                changed |= ImGui.Combo("##set", ref rule.MacroSet, Macros.Sets);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90);
                if (ImGui.InputInt("##index", ref rule.MacroIndex))
                {
                    rule.MacroIndex = Math.Clamp(rule.MacroIndex, 0, Macros.PerSet - 1);
                    changed = true;
                }
                var name = Macros.Name(rule.MacroSet, rule.MacroIndex);
                if (name.Length > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled(name);
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(110);
                var ifBusy = (int)rule.IfBusy;
                if (ImGui.Combo("##busy", ref ifBusy, IfBusyNames))
                {
                    rule.IfBusy = (IfBusy)ifBusy;
                    changed = true;
                }

                ImGui.TableNextColumn();
                if (ImGui.Button("Test"))
                    plugin.Run(rule, "Test");

                ImGui.TableNextColumn();
                if (ImGui.Button("Delete"))
                    remove = i;

                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        if (remove is int index)
        {
            rules.RemoveAt(index);
            changed = true;
        }
        if (ImGui.Button("Add rule"))
        {
            rules.Add(new Rule());
            changed = true;
        }
        if (changed)
            plugin.Save();
    }

    private static void DrawIdStatus(Rule rule, List<Moodle>? current)
    {
        if (rule.MoodleId.Trim().Length == 0)
            return;
        if (rule.ParsedMoodleId is not Guid id)
        {
            ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), "Not a Moodle ID");
            return;
        }
        var match = current?.Find(m => m.Id == id);
        if (match != null)
            ImGui.TextDisabled(match.Title);
    }

    private void DrawActiveMoodles(List<Moodle>? moodles)
    {
        ImGui.Text("Moodles on you now");
        if (moodles == null)
        {
            ImGui.TextDisabled("Moodles isn't loaded, or you're not logged in.");
            return;
        }
        foreach (var moodle in moodles)
        {
            ImGui.PushID(moodle.Id.ToString());
            if (ImGui.SmallButton("Add rule"))
            {
                plugin.Config.Rules.Add(new Rule { MoodleId = moodle.Id.ToString() });
                plugin.Save();
            }
            ImGui.SameLine();
            ImGui.Text(moodle.Applier.Length > 0 ? $"{moodle.Title}  (from {moodle.Applier})" : moodle.Title);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{moodle.Id}\nRight-click to copy");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImGui.SetClipboardText(moodle.Id.ToString());
            ImGui.PopID();
        }
    }
}
