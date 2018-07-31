using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SharpCraft.Framework;

class LoadMPQ {
    [Import]
    Profile CurrentProfile;

    [Export(typeof(InitializeDelegate))]
    public void Initialize() {
        try {
            var dir = $"{CurrentProfile.PluginsDirectory}\\LoadMPQ";
            if(!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
                File.Create($"{dir}\\place_your_mpq_here!");
                return;
            }

            var archives = Directory.GetFiles(dir, "*.mpq", SearchOption.AllDirectories);
            if(archives.Length < 1)
                return;

            ApplyPatch(archives);
        } catch {
        }
    }
    
    const int
        codeInjOffset = 0x0211A8,
        mpqFuncOffset = 0x3997A0;

    void ApplyPatch(string[] list) {
        var extSz = list.Length * (MovEspDwordSize + CallSize) + exPatch.Length + JmpSize;

        IntPtr
            baseAddr = Process.GetCurrentProcess().MainModule.BaseAddress,
            codeInjAddr = baseAddr + codeInjOffset,
            codeDestAddr = Marshal.AllocHGlobal(extSz),
            mpqFuncAddr = baseAddr + mpqFuncOffset;

        VirtualProtect(codeInjAddr, JmpSize + injPatch.Length, erwFlag, out outPtr);
        VirtualProtect(codeDestAddr, extSz, erwFlag, out outPtr);

        WriteJmp(codeInjAddr + BasePatch(codeInjAddr, codeDestAddr), codeDestAddr + InjectArchives(codeDestAddr, mpqFuncAddr, list));
    }

    byte[] injPatch = { 0x90, 0x90, 0x90 };
    int BasePatch(IntPtr codeInjAddr, IntPtr codeDestAddr) {
        var offset = WriteJmp(codeDestAddr, codeInjAddr);
        Marshal.Copy(injPatch, 0, codeDestAddr + offset, injPatch.Length);
        offset += injPatch.Length;
        return offset;
    }

    byte[] exPatch = { 0x83, 0xC4, 0x10, 0xB8, 0x01, 0x00, 0x00, 0x00 };
    int InjectArchives(IntPtr codeDestAddr, IntPtr mpqFuncAddr, string[] list) {
        var offset = 0;
        for(int i = 0; i < list.Length; i++) {
            offset += WriteMovEspDword(Marshal.StringToCoTaskMemAnsi(list[i]), codeDestAddr + offset);
            offset += WriteCall(mpqFuncAddr, codeDestAddr + offset);
        }
        Marshal.Copy(exPatch, 0, codeDestAddr + offset, exPatch.Length);
        offset += exPatch.Length;
        return offset;
    }

    int JmpSize = IntPtr.Size + 1;
    int WriteJmp(IntPtr jmpAddr, IntPtr writeAddr) {
        var offset = 0;
        Marshal.WriteByte(writeAddr, offset++, 0xE9);
        Marshal.WriteIntPtr(writeAddr, offset, IntPtr.Subtract(jmpAddr, (int)writeAddr + (offset += IntPtr.Size)));
        return offset;
    }

    int CallSize = IntPtr.Size + 1;
    int WriteCall(IntPtr callAddr, IntPtr writeAddr) {
        var offset = 0;
        Marshal.WriteByte(writeAddr, offset++, 0xE8);
        Marshal.WriteIntPtr(writeAddr, offset, IntPtr.Subtract(callAddr, (int)writeAddr + (offset += IntPtr.Size)));
        return offset;
    }

    int MovEspDwordSize = IntPtr.Size + 3;
    int WriteMovEspDword(IntPtr destAddr, IntPtr writeAddr) {
        var offset = 0;
        Marshal.WriteByte(writeAddr, offset++, 0xC7);
        Marshal.WriteByte(writeAddr, offset++, 0x04);
        Marshal.WriteByte(writeAddr, offset++, 0x24);
        Marshal.WriteIntPtr(writeAddr, offset, destAddr);
        offset += IntPtr.Size;
        return offset;
    }

    static readonly IntPtr erwFlag = new IntPtr(0x40);
    IntPtr outPtr = IntPtr.Zero;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtect(IntPtr address, int size, IntPtr protect, out IntPtr oldProtect);
}
