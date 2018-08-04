using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using MindWorX.Generic.PluginStorage;
using MindWorX.War3Editor.Hooks;
using MindWorX.War3Editor.MenuInjection;
using MindWorX.War3Editor.ProcessingInjection;
using Serilog;
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
    
    const string AH = "AdicHelper";
    public string Name { get; } = AH;
    public string Group { get; } = AH;
    public int OrderIndex { get; } = -1;
    
    bool Inited = false;
    string AHPath, AHWorkFolder;
    IBasicMenuItem EnableParser, EnableOptimizer, DebugMode, LocalsFlush, DefaultCjBj, CompatMode, NullBoolexpr;

    public void InstallMenu(IBasicMenu parentMenu) {
        AHWorkFolder = $"{CurrentProfile.PluginsDirectory}\\AdicHelper";
        AHPath = $"{AHWorkFolder}\\AdicHelper.exe";

        if(!File.Exists(AHPath)) {
            Log.Error($"Can't run {AH}: file \"{AHPath}\" not found.");
            return;
        }

        Storage = ImportStorage.GetPluginStorage(typeof(AdicHelper));

        parentMenu.AppendMenuItem(AH, isEnabled: false);

        EnableParser = parentMenu.AppendMenuItem("Enable cJASS", true, Storage.GetValue(nameof(EnableParser), true), true);
        EnableParser.Click += (s, e) => Storage.SetValue(nameof(EnableParser), EnableParser.IsChecked);
        
        EnableOptimizer = parentMenu.AppendMenuItem("Enable optimizer", true, Storage.GetValue(nameof(EnableOptimizer), true), true);
        EnableOptimizer.Click += (s, e) => Storage.SetValue(nameof(EnableOptimizer), EnableOptimizer.IsChecked);
        
        DebugMode = parentMenu.AppendMenuItem("Enable debug", true, Storage.GetValue(nameof(DebugMode), false), true);
        DebugMode.Click += (s, e) => Storage.SetValue(nameof(DebugMode), DebugMode.IsChecked);

        LocalsFlush = parentMenu.AppendMenuItem("Locals auto flush", true, Storage.GetValue(nameof(LocalsFlush), true), true);
        LocalsFlush.Click += (s, e) => Storage.SetValue(nameof(LocalsFlush), LocalsFlush.IsChecked);

        DefaultCjBj = parentMenu.AppendMenuItem("Compile for default cj and bj", true, Storage.GetValue(nameof(DefaultCjBj), true), true);
        DefaultCjBj.Click += (s, e) => Storage.SetValue(nameof(DefaultCjBj), DefaultCjBj.IsChecked);

        CompatMode = parentMenu.AppendMenuItem("Modules compatibility mode", true, Storage.GetValue(nameof(CompatMode), true), true);
        CompatMode.Click += (s, e) => Storage.SetValue(nameof(CompatMode), CompatMode.IsChecked);

        NullBoolexpr = parentMenu.AppendMenuItem("Use 'null' as default boolexpr", true, Storage.GetValue(nameof(NullBoolexpr), true), true);
        NullBoolexpr.Click += (s, e) => Storage.SetValue(nameof(NullBoolexpr), NullBoolexpr.IsChecked);

        parentMenu.AppendMenuItem("About AdicHelper...", false, false, true).Click += (s, e) => Process.Start(AHPath);

        Hooks.MapSaved += Hooks_MapSaved;

        Inited = true;
    }

    public bool CompileScript(string scriptPath, bool savingMap) => !(Inited && EnableParser.IsChecked && savingMap);

    public bool CompileMap(string mapPath) {
        if(!(Inited && EnableParser.IsChecked))
            return true;

        var proc = Process.Start(new ProcessStartInfo {
            FileName = AHPath,
            WorkingDirectory = AHWorkFolder,
            Arguments = $"/mappars=\"{mapPath}\"{(DebugMode.IsChecked ? " /dbg" : "")}{(LocalsFlush.IsChecked ? " /alf" : "")}{(DefaultCjBj.IsChecked ? " /ibj=\"0\" /icj=\"0\"" : "")}{(CompatMode.IsChecked ? " /mcm" : "")}{(NullBoolexpr.IsChecked ? " /dbt" : "")}",
        });
        proc.WaitForExit();

        return proc.ExitCode == 0;
    }

    void Hooks_MapSaved(object sender, MapSavedEventArgs e) {
        if(e.Result && Inited && EnableParser.IsChecked && EnableOptimizer.IsChecked) {
            Process.Start(new ProcessStartInfo {
                FileName = AHPath,
                WorkingDirectory = AHWorkFolder,
                Arguments = $"/mapoptz=\"{e.FileName}\"",
            }).WaitForExit();
        }
    }
}
