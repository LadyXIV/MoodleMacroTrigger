using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace MoodleMacroTrigger;

public static unsafe class Macros
{
    public static readonly string[] Sets = ["Individual", "Shared"];
    public const int PerSet = 100;

    public static bool Run(int set, int index)
    {
        var macro = Get(set, index);
        if (macro == null || !macro->IsNotEmpty())
            return false;
        RaptureShellModule.Instance()->ExecuteMacro(macro);
        return true;
    }

    public static string Name(int set, int index)
    {
        var macro = Get(set, index);
        return macro == null ? "" : macro->Name.ToString();
    }

    private static RaptureMacroModule.Macro* Get(int set, int index)
    {
        var module = RaptureMacroModule.Instance();
        if (module == null || set is < 0 or > 1 || index is < 0 or >= PerSet)
            return null;
        return module->GetMacro((uint)set, (uint)index);
    }
}
