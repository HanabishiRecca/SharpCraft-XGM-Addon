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
        int
            baseAddr = (int)Process.GetCurrentProcess().MainModule.BaseAddress,
            codeInjAddr = baseAddr + codeInjOffset,
            mpqFuncAddr = baseAddr + mpqFuncOffset;
        
        if(!Check(codeInjAddr, mpqFuncAddr)) {
            Log.Error("LoadMPQ: Patch pattern mismatch (wrong game version?)");
            return;
        }

        Log.Information("LoadMPQ: Injecting mpq archives");

        int
            extSz = sizeof(JmpOp) + ((sizeof(JmpOp) + sizeof(MovEspDwordOp)) * list.Length) + sizeof(JmpOp),
            codeDestAddr = (int)Marshal.AllocHGlobal(extSz);

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

    unsafe bool Check(int codeInjAddr, int mpqFuncAddr) {
        return JmpOp.Compare(*(JmpOp*)codeInjAddr, new JmpOp(mpqFuncAddr - (codeInjAddr + sizeof(JmpOp)), true));
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct JmpOp {
        byte OpCode;
        int Offset;

        const byte
            callOpCode = 0xE8,
            jmpOpCode = 0xE9;

        public static unsafe void Write(int writeAddr, int jmpAddr, bool call) {
            var op = (JmpOp*)writeAddr;
            op->OpCode = call ? callOpCode : jmpOpCode;
            op->Offset = jmpAddr - (writeAddr + sizeof(JmpOp));
        }

        public JmpOp(int offset, bool call) {
            OpCode = call ? callOpCode : jmpOpCode;
            Offset = offset;
        }

        public static unsafe bool Compare(JmpOp opA, JmpOp opB) {
            return (opA.OpCode == opB.OpCode) && (opA.Offset == opB.Offset);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct MovEspDwordOp {
        fixed byte OpCode[3];
        int Addr;

        const byte
            opCode0 = 0xC7,
            opCode1 = 0x04,
            opCode2 = 0x24;

        public static unsafe void Write(int writeAddr, int destAddr) {
            var op = (MovEspDwordOp*)writeAddr;
            op->OpCode[0] = opCode0;
            op->OpCode[1] = opCode1;
            op->OpCode[2] = opCode2;
            op->Addr = destAddr;
        }
    }

    static readonly int erwFlag = 0x40;
    int outPtr = 0;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtect(int address, int size, int protect, out int oldProtect);
}
