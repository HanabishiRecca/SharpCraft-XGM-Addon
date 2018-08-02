using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SharpCraft.Framework;

class WETempFolderIssueWorkaround {
    [Export(typeof(InitializeDelegate))]
    public void Initialize() {
        var baseAddr = Process.GetCurrentProcess().MainModule.BaseAddress;
        for(int i = 0; i < Procedures.Length; i++) {
            var destAddr = baseAddr + Procedures[i];
            VirtualProtect(destAddr, CallPatchSize, erwFlag, out outPtr);
            FillNOP(destAddr, CallPatchSize);
        }
    }

    void FillNOP(IntPtr addr, int count) {
        for(int i = 0; i < count; i++)
            Marshal.WriteByte(addr, i, 0x90);
    }

    readonly int[] Procedures = { 0x216DF, 0x2255A };
    readonly int CallPatchSize = IntPtr.Size + 1;

    static readonly IntPtr erwFlag = new IntPtr(0x40);
    IntPtr outPtr = IntPtr.Zero;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtect(IntPtr address, int size, IntPtr protect, out IntPtr oldProtect);
}
