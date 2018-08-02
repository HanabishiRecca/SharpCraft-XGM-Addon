using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.InteropServices;
using SharpCraft.Framework;
using StormLibSharp;

class UMSWE {
    [Import]
    Profile CurrentProfile;

    [Export(typeof(InitializeDelegate))]
    public void Initialize() {
        try {
            TempFolder = $"{Path.GetTempPath()}\\UMSWE_TEMP";
            WETempFolderIssueWorkaround();

            var loadmpq = $"{CurrentProfile.PluginsDirectory}\\LoadMPQ";
            Directory.CreateDirectory(loadmpq);

            ArchivePath = $"{loadmpq}\\{UmsweArchiveName}";
            UmswePath = $"{CurrentProfile.PluginsDirectory}\\UMSWE";
            IntegrityPath = $"{loadmpq}\\umswe_integrity";
            MasterPath = $"{CurrentProfile.WorkingDirectory}\\{MasterArchiveName}";
            LocalePath = $"{CurrentProfile.WorkingDirectory}\\{LocaleArchiveName}";

            if(CheckIntegrity())
                return;

            Directory.CreateDirectory(TempFolder);
            BuildDataFiles();
            BuildArchive();
            StoreIntegrity();
            Directory.Delete(TempFolder, true);
        } catch(Exception e) {
            File.WriteAllText("info.log", e.ToString());
        }
    }

    const string
        MasterArchiveName = "War3x.mpq",
        LocaleArchiveName = "War3xLocal.mpq",
        UmsweArchiveName = "umswe.mpq";

    string TempFolder, UmswePath, ArchivePath, IntegrityPath, MasterPath, LocalePath;
    
    string[] DataFiles = { "MiscData.txt", "TriggerData.txt", "TriggerStrings.txt", "WorldEditData.txt", "WorldEditLayout.txt", "WorldEditStrings.txt" };
    void BuildDataFiles() {
        using(var master = new MpqArchive(MasterPath, FileAccess.Read)) {
            using(var locale = new MpqArchive(LocalePath, FileAccess.Read)) {
                for(int i = 0; i < DataFiles.Length; i++) {
                    string
                        file = DataFiles[i],
                        pathInMpq = $"UI\\{file}",
                        tmpPath = $"{TempFolder}\\{file}",
                        dataPath = $"{UmswePath}\\Data\\{file}",
                        patch = $"{dataPath}.patch",
                        addon = $"{dataPath}.addon";

                    //Why HasFile doesn't work?
                    /*if(locale.HasFile(pathInMpq)) {
                        locale.ExtractFile(pathInMpq, tmpPath);
                    } else if(master.HasFile(pathInMpq)) {
                        master.ExtractFile(pathInMpq, tmpPath);
                    }*/

                    try {
                        locale.ExtractFile(pathInMpq, tmpPath);
                    } catch {
                        master.ExtractFile(pathInMpq, tmpPath);
                    }

                    var data = new DataFile(tmpPath);
                    if(File.Exists(patch))
                        data.ApplyPatch(patch, PatchMode.Patch);
                    if(File.Exists(addon))
                        data.ApplyPatch(addon, PatchMode.Addon);

                    data.SaveAs(tmpPath);
                }
            }
        }
    }
    
    const uint Revision = 1;
    void BuildArchive() {
        File.Delete(ArchivePath);
        var handle = IntPtr.Zero;
        SFileCreateArchive(ArchivePath, 0, 20, out handle);
        SFileCloseArchive(handle);

        using(var mpq = new MpqArchive(ArchivePath, FileAccess.ReadWrite)) {
            mpq.AddListFile("");
            for(int i = 0; i < DataFiles.Length; i++) {
                var file = DataFiles[i];
                mpq.AddFileFromDisk($"{TempFolder}\\{file}", $"UI\\{file}");
            }

            var otherFiles = Directory.GetFiles(UmswePath);
            for(int i = 0; i < otherFiles.Length; i++) {
                var file = otherFiles[i];
                mpq.AddFileFromDisk(file, Path.GetFileName(file));
            }

            mpq.Dispose();
        }
    }
    
    bool CheckIntegrity() {
        try {
            if(!(File.Exists(ArchivePath) && File.Exists(IntegrityPath)))
                return false;

            using(var fs = new FileStream(IntegrityPath, FileMode.Open)) {
                using(var reader = new BinaryReader(fs)) {
                    long
                        rev = reader.ReadUInt32(),
                        umsweSz = reader.ReadInt64(),
                        masterSz = reader.ReadInt64(),
                        localeSz = reader.ReadInt64();

                    reader.Close();

                    if(!((Revision == rev) && (new FileInfo(ArchivePath).Length == umsweSz) && (new FileInfo(MasterPath).Length == masterSz) && (new FileInfo(LocalePath).Length == localeSz)))
                        return false;
                }
                fs.Close();
            }
        } catch {
            return false;
        }

        return true;
    }

    void StoreIntegrity() {
        using(var fs = new FileStream(IntegrityPath, FileMode.Create)) {
            using(var writer = new BinaryWriter(fs)) {
                writer.Write(Revision);
                writer.Write(new FileInfo(ArchivePath).Length);
                writer.Write(new FileInfo(MasterPath).Length);
                writer.Write(new FileInfo(LocalePath).Length);
                writer.Close();
            }
            fs.Close();
        }
    }

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileCreateArchive([MarshalAs(UnmanagedType.LPTStr)] string szMpqName, uint dwCreateFlags, uint dwMaxFileCount, out IntPtr phMpq);

    [DllImport("stormlib.dll", ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = false)]
    public static extern bool SFileCloseArchive(IntPtr hMpq);

    //World Editor temp folder issue workaround. THANKS BLIZZARD. Details: https://us.battle.net/forums/en/bnet/topic/20766887191
    FileStream TmpLock;
    void WETempFolderIssueWorkaround() {
        var newPath = $"{Path.GetTempPath()}\\WorldEditorTemp";
        Environment.SetEnvironmentVariable("TMP", newPath);
        Environment.SetEnvironmentVariable("TEMP", newPath);
        Directory.CreateDirectory(newPath);
        TmpLock = new FileStream($"{newPath}\\lock", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    }
}
