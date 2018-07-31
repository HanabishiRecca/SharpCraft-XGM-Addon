using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using MindWorX.Generic.PluginStorage;
using MindWorX.War3Editor.Hooks;
using MindWorX.War3Editor.MenuInjection;
using MindWorX.War3Editor.ProcessingInjection;
using SharpCraft.Framework;

[Export(typeof(ICompiler)), Export(typeof(ICompilerMenuProvider))]
class AdicHelper : ICompiler, ICompilerMenuProvider {
    [Import]
    Profile CurrentProfile;

    [Import]
    ISimplePluginStorage ImportStorage;

    [Import]
    IWar3EditorHooks Hooks;

    PluginStorage Storage;
    string AHPath, AHWorkFolder;

    const string AH = "AdicHelper";
    public string Name { get; } = AH;
    public string Group { get; } = AH;
    public int OrderIndex { get; } = -1;
    
    bool ParserEnabled {
        get => Storage.GetValue(nameof(ParserEnabled), true);
        set => Storage.SetValue(nameof(ParserEnabled), value);
    }

    bool OptimizerEnabled {
        get => Storage.GetValue(nameof(OptimizerEnabled), true);
        set => Storage.SetValue(nameof(OptimizerEnabled), value);
    }

    bool DebugMode {
        get => Storage.GetValue(nameof(DebugMode), false);
        set => Storage.SetValue(nameof(DebugMode), value);
    }

    bool LocalsFlush {
        get => Storage.GetValue(nameof(LocalsFlush), true);
        set => Storage.SetValue(nameof(LocalsFlush), value);
    }

    bool DefaultCjBj {
        get => Storage.GetValue(nameof(DefaultCjBj), true);
        set => Storage.SetValue(nameof(DefaultCjBj), value);
    }

    bool CompatMode {
        get => Storage.GetValue(nameof(CompatMode), true);
        set => Storage.SetValue(nameof(CompatMode), value);
    }

    bool NullBoolexpr {
        get => Storage.GetValue(nameof(NullBoolexpr), true);
        set => Storage.SetValue(nameof(NullBoolexpr), value);
    }

    bool Inited = false;
    public void InstallMenu(IBasicMenu parentMenu) {
        AHWorkFolder = $"{CurrentProfile.PluginsDirectory}\\AdicHelper";
        AHPath = $"{AHWorkFolder}\\AdicHelper.exe";

        if(!File.Exists(AHPath))
            return;

        Storage = ImportStorage.GetPluginStorage(typeof(AdicHelper));

        parentMenu.AppendMenuItem(AH, isEnabled: false);
        parentMenu.AppendMenuItem("Enable AdicParser", true, ParserEnabled, true).Click += (sender, e) => ParserEnabled = (sender as IBasicMenuItem).IsChecked;
        parentMenu.AppendMenuItem("Enable AdicOptimizer", true, OptimizerEnabled, true).Click += (sender, e) => OptimizerEnabled = (sender as IBasicMenuItem).IsChecked;
        parentMenu.AppendMenuItem("Enable debug", true, DebugMode, true).Click += (sender, e) => DebugMode = (sender as IBasicMenuItem).IsChecked;
        parentMenu.AppendMenuItem("Locals auto flush", true, LocalsFlush, true).Click += (sender, e) => LocalsFlush = (sender as IBasicMenuItem).IsChecked;
        parentMenu.AppendMenuItem("Compile for default cj and bj", true, DefaultCjBj, true).Click += (sender, e) => DefaultCjBj = (sender as IBasicMenuItem).IsChecked;
        parentMenu.AppendMenuItem("Modules compatibility mode", true, CompatMode, true).Click += (sender, e) => CompatMode = (sender as IBasicMenuItem).IsChecked;
        parentMenu.AppendMenuItem("Use 'null' as default boolexpr", true, NullBoolexpr, true).Click += (sender, e) => NullBoolexpr = (sender as IBasicMenuItem).IsChecked;

        parentMenu.AppendMenuItem("About AdicHelper...", false, false, true).Click += (sender, e) => Process.Start(AHPath);

        Hooks.MapSaved += Hooks_MapSaved;

        Inited = true;
    }

    public bool CompileScript(string scriptPath, bool savingMap) => !Inited || !ParserEnabled || savingMap;

    public bool CompileMap(string mapPath) {
        if(!Inited || !ParserEnabled)
            return true;

        var agrs = $"/mappars=\"{mapPath}\" ";

        if(DebugMode)
            agrs += " /dbg";

        if(LocalsFlush)
            agrs += " /alf";

        if(DefaultCjBj)
            agrs += " /ibj=\"0\" /icj=\"0\"";

        if(CompatMode)
            agrs += " /mcm";

        if(NullBoolexpr)
            agrs += " /dbt";

        var proc = Process.Start(new ProcessStartInfo {
            FileName = AHPath,
            WorkingDirectory = AHWorkFolder,
            Arguments = agrs,
        });
        proc.WaitForExit();

        return proc.ExitCode == 0;
    }

    void Hooks_MapSaved(object sender, MapSavedEventArgs e) {
        if(e.Result && ParserEnabled && OptimizerEnabled) {
            Process.Start(new ProcessStartInfo {
                FileName = AHPath,
                WorkingDirectory = AHWorkFolder,
                Arguments = $"/mapoptz=\"{e.FileName}\"",
            }).WaitForExit();
        }
    }
}
