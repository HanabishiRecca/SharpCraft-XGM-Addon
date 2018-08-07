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
                Log.Information("WETemp: Procedure found, applying workaround");
                FillNOP(procAddr, sizeof(CallOp));
            }
        }
    }
    
    const int srcFunc = 0x112B0;
    unsafe bool Check(int baseAddr, int procAddr) {
        return CallOp.Compare(*(CallOp*)procAddr, new CallOp((baseAddr + srcFunc) - (procAddr + sizeof(CallOp))));
    }

    unsafe void FillNOP(int addr, int size) {
        VirtualProtect(addr, size, erwFlag, out outPtr);

        var mem = (byte*)addr;
        for(int i = 0; i < size; i++)
            mem[i] = 0x90;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct CallOp {
        byte OpCode;
        int Offset;

        const byte callOp = 0xE8;

        public CallOp(int offset) {
            OpCode = callOp;
            Offset = offset;
        }

        public static unsafe bool Compare(CallOp opA, CallOp opB) {
            return (opA.OpCode == opB.OpCode) && (opA.Offset == opB.Offset);
        }
    }

    static readonly int erwFlag = 0x40;
    int outPtr = 0;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtect(int address, int size, int protect, out int oldProtect);
}
