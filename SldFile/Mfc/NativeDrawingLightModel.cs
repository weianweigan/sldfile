using System;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moLightFeature_c::Serialize 0x4BC5B850, in serializeOldLights mode
// (moModel_c::serializeOldLights 0x4B5F0190 clears the native feature flag).
// This is deliberately NOT a Definition/moModelFeature serializer.
internal sealed class NativeDrawingLightModel : IMfcSerializable
{
    internal bool Directional;
    internal double[] Scalars = new double[3];
    internal uint Color;
    internal int[] States = new int[4];
    internal string? DisplayName, Name;
    internal int Legacy37C, State380, State37C, State480, Id;
    internal double Scalar470, Scalar488, Scalar490;
    internal DrawingPreviewPoint Direction;
    internal NativeDrawingUserProperties Properties = new NativeDrawingUserProperties();

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 0x11F6) throw new NotSupportedException("Unverified legacy light version.");
        for (int i = 0; i < Scalars.Length; i++) ar.F64(ref Scalars[i]);
        ar.U32(ref Color);
        for (int i = 0; i < States.Length; i++) ar.I32(ref States[i]);
        DrawingPreviewRecordModel.String(ar, ref DisplayName); DrawingPreviewRecordModel.String(ar, ref Name);
        ar.I32(ref Legacy37C); ar.I32(ref State380); ar.I32(ref State37C);
        ar.F64(ref Scalar470); ar.I32(ref State480); ar.F64(ref Scalar488);
        Properties.Serialize(ar); ar.I32(ref Id);
        if (Directional) { DrawingPreviewRecordModel.Point(ar, ref Direction); ar.F64(ref Scalar490); }
    }
}

// moUserProperty_c restore/save 0x3CB2EDD0/0x3CB2EED0;
// moPropertyExt payload restore/save 0x3CB2ED10/0x3CB2EE50.
internal sealed class NativeDrawingUserProperties : IMfcSerializable
{
    internal sealed class Entry
    {
        internal uint Type;
        internal float Real;
        internal int Integer;
        internal string? Text;
    }
    internal Entry[] Entries = Array.Empty<Entry>();
    public void Serialize(MfcArchive ar)
    {
        int count = Entries.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) Entries = new Entry[count];
        for (int i = 0; i < Entries.Length; i++)
        {
            if (ar.IsLoading) Entries[i] = new Entry();
            var item = Entries[i]; ar.U32(ref item.Type);
            // The native switch has no payload for other type values.
            switch (item.Type)
            {
                case 0: ar.F32(ref item.Real); break;
                case 1: ar.I32(ref item.Integer); break;
                case 2: DrawingPreviewRecordModel.String(ar, ref item.Text); break;
            }
        }
    }
}
