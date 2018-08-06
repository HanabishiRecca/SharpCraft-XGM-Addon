using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;
using SharpCraft.Framework;

class LoadMPQ {
    [Import]
    Profile CurrentProfile;

    [Export(typeof(InitializeDelegate))]
    public void Initialize() {
        var dir = $"{CurrentProfile.PluginsDirectory}\\LoadMPQ";
        if(!Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
            File.Create($"{dir}\\place_your_mpq_here!");
            return;
        }

        var archives = Directory.GetFiles(dir, "*.mpq", SearchOption.AllDirectories);
        if(archives.Length < 1)
            return;

        var cd = Directory.GetCurrentDirectory();
        for(int i = 0; i < archives.Length; i++) {
            var path = archives[i];
            if(path.StartsWith(cd))
                archives[i] = path.Substring(cd.Length + 1);
        }

        ApplyPatch(archives);
    }
    
    const int
        codeInjOffset = 0x0211A3,
        mpqFuncOffset = 0x3997A0;

    unsafe void ApplyPatch(string[] list) {
        var extSz = sizeof(JmpOp) + ((sizeof(JmpOp) + sizeof(MovEspDwordOp)) * list.Length) + sizeof(JmpOp);

        int
            baseAddr = (int)Process.GetCurrentProcess().MainModule.BaseAddress,
            codeInjAddr = baseAddr + codeInjOffset,
            mpqFuncAddr = baseAddr + mpqFuncOffset;

        var injData = (JmpOp*)codeInjAddr;
        if(!((injData->OpCode == JmpOp.callOp) && (injData->Offset == (mpqFuncAddr - (codeInjAddr + sizeof(JmpOp)))))) {
            Log.Error("LoadMPQ: Unable to inject code - patch pattern mismatch (wrong game version?)");
            return;
        }

        var codeDestAddr = (int)Marshal.AllocHGlobal(extSz);

        VirtualProtect(codeInjAddr, sizeof(JmpOp), erwFlag, out outPtr);
        JmpOp.Write(codeInjAddr, codeDestAddr, false);

        VirtualProtect(codeDestAddr, extSz, erwFlag, out outPtr);
        var writeAddr = codeDestAddr;
        JmpOp.Write(writeAddr, mpqFuncAddr, true);
        writeAddr += sizeof(JmpOp);

        for(int i = 0; i < list.Length; i++) {
            MovEspDwordOp.Write(writeAddr, (int)Marshal.StringToHGlobalAnsi(list[i]));
            writeAddr += sizeof(MovEspDwordOp);
            JmpOp.Write(writeAddr, mpqFuncAddr, true);
            writeAddr += sizeof(JmpOp);
        }
        
        JmpOp.Write(writeAddr, codeInjAddr + sizeof(JmpOp), false);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct JmpOp {
        public byte OpCode;
        public int Offset;

        public const byte
            callOp = 0xE8,
            jmpOp = 0xE9;

        public unsafe static void Write(int writeAddr, int jmpAddr, bool call) {
            var command = (JmpOp*)writeAddr;
            command->OpCode = call ? callOp : jmpOp;
            command->Offset = jmpAddr - (writeAddr + sizeof(JmpOp));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct MovEspDwordOp {
        byte OpCode0, OpCode1, OpCode2;
        int Addr;

        const byte
            op0 = 0xC7,
            op1 = 0x04,
            op2 = 0x24;

        public unsafe static void Write(int writeAddr, int destAddr) {
            var command = (MovEspDwordOp*)writeAddr;
            command->OpCode0 = op0;
            command->OpCode1 = op1;
            command->OpCode2 = op2;
            command->Addr = destAddr;
        }
    }

    static readonly int erwFlag = 0x40;
    int outPtr = 0;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtect(int address, int size, int protect, out int oldProtect);
}
