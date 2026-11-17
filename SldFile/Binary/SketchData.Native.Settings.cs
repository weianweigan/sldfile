using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal ushort NativeState12C, NativeState128;
    internal int NativeState130;
    internal NativeSettings Settings = new NativeSettings();

    // sgSketch follows the four post-relation tables with these states and
    // its inline +140 settings object. Further flags/cache data still follow.
    internal void SerializeNativeSettingsPrefix(MfcArchive ar)
    {
        ar.U16(ref NativeState12C); ar.U16(ref NativeState128); ar.I32(ref NativeState130);
        Settings.Serialize(ar);
    }

    internal sealed class NativeSettings
    {
        internal int State8, StateC, State10, State14, State18, State1C, State20, State24,
            State28, State2C, State30, State34, State38, State40, State3C;
        // +140 virtual Serialize -> 0x4C5AF7A0, vtable 0x4D1B61B8.
        // State40 precedes State3C on the wire. This is an inline CObject
        // payload, not a newly declared MFC class or fifteen guessed bools.
        internal void Serialize(MfcArchive ar)
        {
            if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch settings.");
            ar.I32(ref State8); ar.I32(ref StateC); ar.I32(ref State10); ar.I32(ref State14);
            if (ar.DocumentVersion > 0x1407) ar.I32(ref State18);
            ar.I32(ref State1C); ar.I32(ref State20); ar.I32(ref State24); ar.I32(ref State28);
            ar.I32(ref State2C); ar.I32(ref State30); ar.I32(ref State34); ar.I32(ref State38);
            ar.I32(ref State40); ar.I32(ref State3C);
        }
    }
}
