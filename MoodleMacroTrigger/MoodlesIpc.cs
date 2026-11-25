using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

using MoodleInfo = (
    int Version, System.Guid GUID, int IconID, string Title, string Description,
    string CustomVFXPath, long ExpireTicks, int Type, int Stacks, int StackSteps,
    uint Modifiers, System.Guid ChainedStatus, int ChainTrigger, string Applier,
    string Dispeller, bool Permanent);

namespace MoodleMacroTrigger;

public sealed record Moodle(Guid Id, string Title, string Applier);

public sealed partial class MoodlesIpc : IDisposable
{
    private readonly ICallGateSubscriber<object> ready;
    private readonly ICallGateSubscriber<nint, object> statusManagerModified;
    private readonly ICallGateSubscriber<List<MoodleInfo>> getClientInfo;
    private readonly Action onReady;
    private readonly Action<nint> onModified;

    public MoodlesIpc(IDalamudPluginInterface pluginInterface, Action onReady, Action<nint> onModified)
    {
        ready = pluginInterface.GetIpcSubscriber<object>("Moodles.Ready");
        statusManagerModified = pluginInterface.GetIpcSubscriber<nint, object>("Moodles.StatusManagerModified");
        getClientInfo = pluginInterface.GetIpcSubscriber<List<MoodleInfo>>("Moodles.GetClientStatusManagerInfoV2");
        this.onReady = onReady;
        this.onModified = onModified;
        ready.Subscribe(onReady);
        statusManagerModified.Subscribe(onModified);
    }

    public List<Moodle>? ClientMoodles()
    {
        if (!getClientInfo.HasFunction)
            return null;
        return getClientInfo.InvokeFunc()
            .Select(info => new Moodle(info.GUID, StripFormatting(info.Title), info.Applier ?? ""))
            .ToList();
    }

    public static string StripFormatting(string title) => FormattingTag().Replace(title ?? "", "").Trim();

    [GeneratedRegex(@"\[color=[0-9a-zA-Z]+\]|\[/color\]|\[glow=[0-9a-zA-Z]+\]|\[/glow\]|\[i\]|\[/i\]", RegexOptions.IgnoreCase)]
    private static partial Regex FormattingTag();

    public void Dispose()
    {
        statusManagerModified.Unsubscribe(onModified);
        ready.Unsubscribe(onReady);
    }
}
