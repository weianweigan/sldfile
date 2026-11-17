using System;
using System.IO;

namespace SldFile.Mfc;

// sldmfcu moDetailDefs_c::Serialize 0x3CAFF880, modern layout >=5000.
// This prefix is deliberately not registered as a complete MFC object.
// Remaining inline symbol helpers and trailing settings are required payload.
internal sealed partial class NativeDrawingDetailDefinitionsModel
{
    internal int State10, State24, State1388, State138, StateE8, State150, State154;
    internal int State158, State164, State168, State16C, State170, State174;
    internal int State138C, State1390, State1394, State1398, State4C, State48;
    internal int State2BC8, State2BE0, State2BF0, State178, State17C;
    internal int State1A8, State1AC, State1B0, State1B4, State180, State184, State188;
    internal int State18C, State190, State194, State198, State1A0, State1A4;
    internal uint State20;
    internal double State28, State8, State140, State148, State2BC0;
    internal byte State30, State38, State14, State18, State1C, State34, State3C, State40, State44;
    internal ushort StateE0, StateE2;
    internal string? State13A0, State2BD8, State2BE8;
    internal uint[] StatesEC = new uint[3], States110 = new uint[3];
    internal double[] ValuesF8 = new double[3], Values120 = new double[3];
    internal int[] LayoutStates = Array.Empty<int>();
    internal string?[] Exclusions = Array.Empty<string?>();
    internal NativeDrawingStringArrayModel StringsB8 = new NativeDrawingStringArrayModel();
    internal NativeDrawingCharacterFormatModel CharacterFormat = new NativeDrawingCharacterFormatModel();
    internal NativeDrawingDimensionDefinitionsModel Dimensions = new NativeDrawingDimensionDefinitionsModel();
    internal NativeDrawingAnnotationDefinitionsModel Annotations = new NativeDrawingAnnotationDefinitionsModel();
    internal NativeDrawingTableDefinitionsModel Tables = new NativeDrawingTableDefinitionsModel();
    internal NativeDrawingLabelDefinitionsModel Labels = new NativeDrawingLabelDefinitionsModel();

    internal void SerializePrefix(MfcArchive ar)
    {
        SerializeScalarPrefix(ar);
        CharacterFormat.Serialize(ar);
        Dimensions.Serialize(ar);
        Annotations.Serialize(ar);
        Tables.Serialize(ar);
        Labels.Serialize(ar);
    }

    internal void SerializeScalarPrefix(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        if (v < 5000) throw new NotSupportedException("Unverified legacy detail definitions.");
        if (ar.Context.NativeDrawingArchiveConditions != 0)
            throw new NotSupportedException("Detail definition export layout is not decoded.");
        ar.I32(ref State10); DrawingPreviewRecordModel.String(ar, ref State13A0);
        ar.F64(ref State28); ar.Byte(ref State30); ar.U32(ref State20); ar.I32(ref State24);
        ar.F64(ref State8); ar.Byte(ref State38); ar.Byte(ref State14); ar.Byte(ref State18);
        ar.Byte(ref State1C); ar.Byte(ref State34); ar.I32(ref State1388);
        Fixed(ar, StatesEC, 3); Fixed(ar, ValuesF8, 3);
        Fixed(ar, States110, 3); Fixed(ar, Values120, 3);
        // Before 0x23EB the reader converts this state into a boolean.
        // Keep the original filed integer for lossless backward serialization.
        ar.I32(ref State138);
        if (v >= 0x23EB) { ar.F64(ref State140); ar.F64(ref State148); }
        ar.I32(ref StateE8); ar.I32(ref State150); ar.I32(ref State154);
        ar.I32(ref State158); ar.I32(ref State164); ar.I32(ref State168);
        ar.I32(ref State16C); ar.I32(ref State170); ar.I32(ref State174); ar.Byte(ref State3C);
        ar.I32(ref State138C); ar.I32(ref State1390); ar.I32(ref State1394); ar.I32(ref State1398);
        ar.Byte(ref State40); ar.Byte(ref State44);
        if (v > 0x1406) { ar.U16(ref StateE0); ar.U16(ref StateE2); }
        if (v > 0x1423) { ar.I32(ref State4C); ar.I32(ref State48); }
        if (v > 0x1473) { ar.F64(ref State2BC0); ar.I32(ref State2BC8); }
        if (v > 0x17EC)
        {
            DrawingPreviewRecordModel.String(ar, ref State2BD8); ar.I32(ref State2BE0);
            DrawingPreviewRecordModel.String(ar, ref State2BE8); ar.I32(ref State2BF0);
        }
        if (v > 0x1C10) { ar.I32(ref State178); ar.I32(ref State17C); }
        if (v > 0x23D3) { ar.I32(ref State1A8); ar.I32(ref State1AC); ar.I32(ref State1B0); }
        if (v > 0x2BB3) ar.I32(ref State1B4);
        if (v >= 0x17F7) ar.I32(ref State180);
        if (v >= 0x1BC9) { ar.I32(ref State184); ar.I32(ref State188); }
        if (v >= 0x1BCD) { ar.I32(ref State18C); ar.I32(ref State190); ar.I32(ref State194); }
        if (v >= 0x17FF)
        {
            int count = Exclusions.Length; NativeDrawingModelManagersModel.Count(ar, ref count, 1);
            if (ar.IsLoading) Exclusions = new string?[count];
            for (int i = 0; i < count; i++) DrawingPreviewRecordModel.String(ar, ref Exclusions[i]);
        }
        if (v >= 0x1FBB) ar.I32(ref State198);
        if (v >= 0x1FD8)
        {
            ar.I32(ref State1A0); ar.I32(ref State1A4);
            // Native migration interval: 10 obsolete integers before 0x2006;
            // 0x2006 has none; 0x2007..0x374E has 2*5 + 3*5 integers;
            // newer versions use 3*8 + 3*8. Preserve all filed states.
            int length = v < 0x2006 ? 10 : v == 0x2006 ? 0 : v <= 0x374E ? 25 : 48;
            if (ar.IsLoading) LayoutStates = new int[length];
            if (LayoutStates.Length != length) throw new InvalidDataException("Invalid detail layout state table length.");
            for (int i = 0; i < length; i++) ar.I32(ref LayoutStates[i]);
        }
        if (v > 0x3B0F) StringsB8.Serialize(ar);
    }

    internal static void Fixed(MfcArchive ar, double[] values, int length)
    {
        if (values.Length != length) throw new InvalidDataException("Invalid native detail scalar array length.");
        for (int i = 0; i < length; i++) ar.F64(ref values[i]);
    }
    internal static void Fixed(MfcArchive ar, uint[] values, int length)
    {
        if (values.Length != length) throw new InvalidDataException("Invalid native detail state array length.");
        for (int i = 0; i < length; i++) ar.U32(ref values[i]);
    }
}
