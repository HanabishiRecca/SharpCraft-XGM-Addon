using System;
using System.IO;
using System.Diagnostics;
using System.ComponentModel.Composition;
using SharpCraft.Framework;
using Microsoft.Win32;

class AutoRegExt {
    [Import]
    Profile CurrentProfile;

    [Import]
    SharpCraftEnvironment CurrentEnvironment;

    [Export(typeof(InitializeDelegate))]
    public void Initialize() {
        try {
            WEPath = Process.GetCurrentProcess().MainModule.FileName;
            if(File.Exists(WEPath)) {
                ShellCommand = $"\"{CurrentEnvironment.Root}\\World Editor Extended.exe\" -loadfile \"%L\"";

                RegExt("w3m", "Scenario", 2);
                RegExt("w3x", "ScenarioEx", 3);
                RegExt("w3n", "Campaign", 4);
                RegExt("wai", "AIData", 5);
            }
        } catch { }
    }
    
    const string RegPath = "HKEY_CURRENT_USER\\SOFTWARE\\Classes";
    string WEPath, ShellCommand;

    void RegExt(string ext, string name, byte icon) {
        var entryName = $"WorldEdit.{name}";
        Registry.SetValue($"{RegPath}\\.{ext}", "", entryName);

        var entryPath = $"{RegPath}\\{entryName}";
        Registry.SetValue($"{entryPath}\\DefaultIcon", "", $"\"{WEPath}\",{icon}");
        Registry.SetValue($"{entryPath}\\shell\\open\\command", "", ShellCommand);
    }

}
