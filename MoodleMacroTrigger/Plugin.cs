using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MoodleMacroTrigger;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/moodlemacro";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IObjectTable objects;
    private readonly IPlayerState player;
    private readonly IPluginLog log;
    private readonly MoodlesIpc moodles;
    private readonly WindowSystem windows = new("MoodleMacroTrigger");
    private readonly ConfigWindow configWindow;

    private HashSet<Guid> active = [];
    private readonly List<(Rule Rule, string Reason)> waiting = [];
    private ulong seededFor;

    public Configuration Config { get; }
    public string LastFired { get; private set; } = "";

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IFramework framework,
                  IObjectTable objects, IPlayerState player, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.framework = framework;
        this.objects = objects;
        this.player = player;
        this.log = log;
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        moodles = new MoodlesIpc(pluginInterface, Seed, OnMoodlesModified);
        configWindow = new ConfigWindow(this);
        windows.AddWindow(configWindow);

        commands.AddHandler(Command, new CommandInfo((_, _) => configWindow.Toggle())
        {
            HelpMessage = "Open settings",
        });
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        pluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        framework.Update += OnUpdate;
    }

    public List<Moodle>? CurrentMoodles()
    {
        try
        {
            return objects.LocalPlayer == null ? null : moodles.ClientMoodles();
        }
        catch (Exception e)
        {
            log.Warning($"Moodles IPC failed: {e.Message}");
            return null;
        }
    }

    public void Save() => pluginInterface.SavePluginConfig(Config);

    // The game refuses to start a macro while another one runs, so a busy rule waits, interrupts or skips.
    public void Run(Rule rule, string reason)
    {
        if (!Macros.IsRunning)
        {
            Execute(rule, reason);
            return;
        }
        switch (rule.IfBusy)
        {
            case IfBusy.Wait:
                waiting.Add((rule, reason));
                Report(rule, reason, "waiting");
                break;
            case IfBusy.Interrupt:
                Macros.Stop();
                waiting.Insert(0, (rule, reason));
                break;
            case IfBusy.Skip:
                Report(rule, reason, "skipped");
                break;
        }
    }

    private void Execute(Rule rule, string reason) =>
        Report(rule, reason, Macros.Run(rule.MacroSet, rule.MacroIndex) ? "" : "empty");

    private void Report(Rule rule, string reason, string outcome)
    {
        LastFired = $"{DateTime.Now:HH:mm:ss}  {reason}: {Macros.Sets[rule.MacroSet]} macro {rule.MacroIndex}{(outcome.Length > 0 ? $" ({outcome})" : "")}";
        log.Information(LastFired);
    }

    private void OnUpdate(IFramework _)
    {
        // Moodles already on the character when it logs in or this plugin loads must not fire.
        if (player.ContentId != 0 && player.ContentId != seededFor && objects.LocalPlayer != null)
            Seed();
        if (waiting.Count > 0 && !Macros.IsRunning)
        {
            var (rule, reason) = waiting[0];
            waiting.RemoveAt(0);
            Execute(rule, reason);
        }
    }

    private void Seed()
    {
        seededFor = player.ContentId;
        waiting.Clear();
        active = CurrentMoodles()?.Select(m => m.Id).ToHashSet() ?? [];
    }

    private void OnMoodlesModified(nint character)
    {
        if (objects.LocalPlayer?.Address != character)
            return;
        if (player.ContentId != seededFor)
        {
            Seed();
            return;
        }
        var current = CurrentMoodles();
        if (current == null)
            return;

        var arrived = current.Where(m => !active.Contains(m.Id)).ToList();
        active = current.Select(m => m.Id).ToHashSet();
        foreach (var moodle in arrived)
            log.Debug($"Moodle applied: {moodle.Title} [{moodle.Id}] from {moodle.Applier}");

        // The game runs one macro at a time, so only the topmost matching rule fires.
        foreach (var rule in Config.Rules)
        {
            var moodle = arrived.FirstOrDefault(rule.Matches);
            if (moodle != null)
            {
                Run(rule, moodle.Title);
                return;
            }
        }
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        pluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        commands.RemoveHandler(Command);
        windows.RemoveAllWindows();
        moodles.Dispose();
    }
}
