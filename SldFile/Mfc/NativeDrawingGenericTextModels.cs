using System;

namespace SldFile.Mfc;

// Native shared run header 0x4B6C0FF0. Keep the two i32 words distinct
// from the class tag supplied by the parent object graph.
internal sealed class NativeDrawingTextRunHeader : IMfcSerializable
{
    internal int State8, StateC;
    public void Serialize(MfcArchive ar) { ar.I32(ref State8); ar.I32(ref StateC); }
}

// moTextDataChar_c vtable 0x4D0A7E68 -> 0x4BF23980, which calls
// space-holder helper 0x4BF24B90 then writes one UTF-16 code unit.
internal class NativeDrawingTextSpaceHolder
{
    internal NativeDrawingTextRunHeader Header = new NativeDrawingTextRunHeader();
    internal float[] Position = new float[3];
    internal float State2C, State38;
    internal ushort State30, State10;
    internal int State34;
    internal void SerializeSpaceHolder(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy text character.");
        Header.Serialize(ar);
        if (Position.Length != 3) throw new System.IO.InvalidDataException("Native character requires three position values.");
        for (int i = 0; i < 3; i++) ar.F32(ref Position[i]);
        ar.F32(ref State2C); ar.U16(ref State30); ar.I32(ref State34); ar.F32(ref State38);
        if (ar.DocumentVersion >= 0x1815) ar.U16(ref State10);
    }
}

internal sealed class NativeDrawingTextCharacterModel : NativeDrawingTextSpaceHolder, IMfcSerializable
{
    internal ushort Character;
    public void Serialize(MfcArchive ar) { SerializeSpaceHolder(ar); ar.U16(ref Character); }
}

// Shared link-to-value base 0x4BF244E0 uses the same space-holder
// payload as character runs, not the unrelated utCharFormat layout.
internal class NativeDrawingTextLink : NativeDrawingTextSpaceHolder
{
    internal int LinkFlags;
    internal string? State48, State50;
    internal NativeDrawingObjectGraph.Node? Objects;
    // Shared link-to-value serializer 0x4BF244E0, also inherited by labels.
    internal void SerializeLink(MfcArchive ar)
    {
        SerializeSpaceHolder(ar); ar.I32(ref LinkFlags);
        DrawingPreviewRecordModel.String(ar, ref State48); DrawingPreviewRecordModel.String(ar, ref State50);
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref Objects);
    }
}

// moTextDataCustomProperty descriptor 0x4DAE94B0, vtable
// 0x4D0A8798 -> 0x4BF23A80 adds its own property tail to this base.
internal sealed class NativeDrawingTextPropertyModel : NativeDrawingTextLink, IMfcSerializable
{
    internal int State68;
    internal string? State70, State80;
    public void Serialize(MfcArchive ar)
    {
        SerializeLink(ar);
        ar.I32(ref State68); DrawingPreviewRecordModel.String(ar, ref State70);
        if (ar.DocumentVersion > 0x1FBC) DrawingPreviewRecordModel.String(ar, ref State80);
    }
}

// moTextDataLabel factory 0x4BF1BE00 -> ctor 0x4BF1F1E0,
// Serialize 0x4BF24160 reuses the link base and appends one i32.
internal sealed class NativeDrawingTextLabelModel : NativeDrawingTextLink, IMfcSerializable
{
    internal int LabelId;
    public void Serialize(MfcArchive ar) { SerializeLink(ar); ar.I32(ref LabelId); }
}

// moTextDataFont_c descriptor 0x4DAE91E0, vtable 0x4D0A93E8
// -> 0x4BF23B70. A text control/run header followed by five CStrings;
// this is distinct from the LOGFONT-style utCharFormat object.
internal sealed class NativeDrawingTextFontModel : IMfcSerializable
{
    internal NativeDrawingTextRunHeader Header = new NativeDrawingTextRunHeader();
    internal string? State10, State18, State20, State28, State30;
    internal int State38;
    internal double State40, State48, State50, State58;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy text-font layout.");
        Header.Serialize(ar);
        DrawingPreviewRecordModel.String(ar, ref State10); DrawingPreviewRecordModel.String(ar, ref State18);
        DrawingPreviewRecordModel.String(ar, ref State20); DrawingPreviewRecordModel.String(ar, ref State28);
        DrawingPreviewRecordModel.String(ar, ref State30);
        if (ar.DocumentVersion > 0x2F49) ar.I32(ref State38);
        ar.F64(ref State40); ar.F64(ref State48); ar.F64(ref State50); ar.F64(ref State58);
    }
}

// moTextDataLineHead_c vtable 0x4D0A92A0 -> 0x4BF241D0.
internal sealed class NativeDrawingTextLineHeadModel : IMfcSerializable
{
    internal NativeDrawingTextRunHeader Header = new NativeDrawingTextRunHeader();
    internal float[] First = new float[3], Second = new float[3];
    internal ushort State10;
    internal NativeDrawingParagraphFormat Paragraph = new NativeDrawingParagraphFormat();
    internal NativeDrawingObjectGraph.Node? Objects;
    internal int State50, State64;
    internal double State58;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy text line head.");
        Header.Serialize(ar); Triple(ar, First); ar.U16(ref State10); Paragraph.Serialize(ar); Triple(ar, Second);
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref Objects); ar.I32(ref State50);
        if (ar.DocumentVersion > 0x1820) ar.F64(ref State58);
        if (ar.DocumentVersion >= 0x1BF6) ar.I32(ref State64);
    }
    private static void Triple(MfcArchive ar, float[] values)
    {
        if (values.Length != 3) throw new System.IO.InvalidDataException("Native text triple requires three values.");
        // 0x4BF231E0 uses F32 from version 0xADA onward.
        for (int i = 0; i < 3; i++) ar.F32(ref values[i]);
    }
}

// moGenTextDataList_c descriptor 0x4DAE92D0, vtable 0x4D0A9B10,
// Serialize 0x4BF23320. Its array is a tagged suObArray, not inline.
internal sealed class NativeDrawingGenericTextListModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? Objects;
    internal int State128;
    internal double State18, State20, State28, State30;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy generic text list.");
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref Objects);
        if (ar.DocumentVersion >= 0x1815) ar.I32(ref State128);
        // Inline helper 0x4BF256D0 is four F64 values.
        ar.F64(ref State18); ar.F64(ref State20); ar.F64(ref State28); ar.F64(ref State30);
    }
}

// sldmfcu utParagraphFormat_c::Serialize 0x3CB20940, inline data.
internal sealed class NativeDrawingParagraphFormat : IMfcSerializable
{
    internal ushort State0, State20, State24;
    internal double State8, State10, State18, State40, State48, State58, State60;
    internal string? State28;
    internal int State30, State38, State50, LegacyState, State68, State6C;
    internal uint State34;
    public void Serialize(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        if (version < 5000) throw new NotSupportedException("Unverified legacy paragraph format.");
        ar.U16(ref State0); ar.F64(ref State8); ar.F64(ref State10); ar.F64(ref State18);
        ar.U16(ref State20); ar.U16(ref State24); DrawingPreviewRecordModel.String(ar, ref State28);
        ar.I32(ref State30); ar.U32(ref State34); ar.I32(ref State38); ar.F64(ref State40);
        if (version >= 0x1429) ar.F64(ref State48);
        if (version >= 0x1BC5)
        {
            ar.I32(ref State50); ar.F64(ref State58); ar.F64(ref State60);
            if (version < 0x1BF6) ar.I32(ref LegacyState);
        }
        if (version > 0x1FC6) { ar.I32(ref State68); ar.I32(ref State6C); }
    }
}
