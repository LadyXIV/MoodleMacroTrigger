using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace MoodleMacroTrigger;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; }
    public List<Rule> Rules = [];
}

public enum IfBusy { Wait, Interrupt, Skip }

[Serializable]
public sealed class Rule
{
    public bool Enabled = true;
    public string MoodleId = "";
    public string Applier = "";
    public int MacroSet;
    public int MacroIndex;
    public IfBusy IfBusy;

    public Guid? ParsedMoodleId => Guid.TryParse(MoodleId, out var id) ? id : null;

    public bool Matches(Moodle moodle) =>
        Enabled && ParsedMoodleId == moodle.Id && MatchesApplier(moodle.Applier);

    private bool MatchesApplier(string applier)
    {
        var wanted = Applier.Trim();
        return wanted.Length == 0
            || string.Equals(wanted, applier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(wanted, applier.Split('@')[0], StringComparison.OrdinalIgnoreCase);
    }
}
