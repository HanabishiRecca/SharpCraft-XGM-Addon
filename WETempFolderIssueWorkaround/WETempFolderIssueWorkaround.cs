using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;
using SharpCraft.Framework;

class WETempFolderIssueWorkaround {
    [Export(typeof(InitializeDelegate))]
    public void Initialize() {
        ApplyFix();
    }

    readonly int[] Procedures = { 0x216DF, 0x2255A };
    unsafe void ApplyFix() {
        var baseAddr = (int)Process.GetCurrentProcess().MainModule.BaseAddress;
        for(int i = 0; i < Procedures.Length; i++) {
            var procAddr = baseAddr + Procedures[i];
            if(Check(baseAddr, procAddr)) {
                Log.Information("WETemp: Workaround procedure found");
                FillNOP(procAddr, sizeof(CallOp));
            }
        }
    }
    
    const int srcFunc = 0x112B0;
    unsafe bool Check(int baseAddr, int procAddr) {
        var call = (CallOp*)procAddr;
        return (call->OpCode == CallOp.callOp) && (call->Offset == ((baseAddr + srcFunc) - (procAddr + sizeof(CallOp))));
    }

    unsafe void FillNOP(int addr, int size) {
        VirtualProtect(addr, size, erwFlag, out outPtr);

        var mem = (byte*)addr;
        for(int i = 0; i < size; i++)
            mem[i] = 0x90;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct CallOp {
        public byte OpCode;
        public int Offset;

        public const byte callOp = 0xE8;
    }

    static readonly int erwFlag = 0x40;
    int outPtr = 0;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtect(int address, int size, int protect, out int oldProtect);
}
