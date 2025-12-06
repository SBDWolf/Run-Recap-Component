using System;
using System.Diagnostics;
namespace CupheadRunRecap.Memory
{
    public interface IFindPointer
    {
        IntPtr FindPointer(Process program, string asmName);
        bool FoundBaseAddress();
        void VerifyPointer(Process program, ref IntPtr pointer);
        PointerVersion Version { get; }
    }
}