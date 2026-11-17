using System;

namespace SldFile.Mfc;

// sldarchiveu moHeader_c::Serialize 0x31D73190. Document metadata remains
// in the native graph: CString arrays, external objects and revision logs can
// be shared with subsequent Definition objects through the archive index.
internal sealed class NativeDrawingHeaderModel : IMfcSerializable
{
    internal int HasContext, Usage, State58, State68, State60, State78, StateC0;
    internal int StateC4, StateC8, State64, HasBounds, State9C, StateA0, StateA4, StateA8, StateAC, StateB0;
    internal int StateE8, StateF0, StateF4, StateF8, StateFC, State100, State104, State108, State10C, State98;
    internal uint HighestId, NextId, State7C, ActiveContent;
    internal uint CreatedTime, UpdatedTime, SavedTime;
    internal int[] ReferenceFlags = new int[3];
    internal double[] Bounds = new double[10];
    internal NativeDrawingObjectGraph.Node? Users, Companies, Logs, ExternalObjects, Self, Paths, ReplacedComponents;

    public void Serialize(MfcArchive ar)
    {
        var graph = NativeDrawingModelFactory.Graph(ar);
        int version = ar.DocumentVersion;
        if (version >= 0x371C) ar.I32(ref HasContext);
        graph.Object(ar, ref Users); graph.Object(ar, ref Companies); graph.Object(ar, ref Logs);
        if (version >= 0xF1) ar.U32(ref UpdatedTime);
        ar.I32(ref Usage); graph.Object(ar, ref ExternalObjects);
        if (version >= 0x97) ar.U32(ref HighestId);
        if (version >= 0xD9) graph.Object(ar, ref Self);
        if (version >= 0x30C) ar.U32(ref NextId);
        if (version >= 0x4E5) ar.I32(ref State58);
        if (version >= 0x501) ar.I32(ref State68);
        if (version >= 0x506) ar.U32(ref SavedTime);
        if (version >= 0x531) ar.I32(ref State60);
        if (version >= 0x85A) ar.I32(ref State78);
        if (version >= 0x91F) ar.I32(ref StateC0);
        if (version >= 0xAA2) graph.Object(ar, ref Paths);
        if (version >= 0xB8D) ar.I32(ref StateC4);
        if (version >= 0xCF7) ar.U32(ref State7C);
        if (version >= 0xE5D) ar.U32(ref CreatedTime);
        if (version >= 0xF3B) graph.Object(ar, ref ReplacedComponents);
        if (version >= 0x1290) ar.I32(ref StateC8);
        if (version >= 0x1304) ar.I32(ref State64);
        if (version >= 0x130F)
        {
            ar.I32(ref HasBounds);
            if (HasBounds != 0) for (int i = 0; i < Bounds.Length; i++) ar.F64(ref Bounds[i]);
        }
        if (version >= 0x2784) for (int i = 0; i < ReferenceFlags.Length; i++) ar.I32(ref ReferenceFlags[i]);
        if (version >= 0x2BC3) ar.U32(ref ActiveContent);
        if (version >= 0x2FA4)
        {
            ar.I32(ref State9C); ar.I32(ref StateA0); ar.I32(ref StateA4);
            ar.I32(ref StateA8); ar.I32(ref StateAC); ar.I32(ref StateB0);
        }
        if (version >= 0x3370) ar.I32(ref StateE8);
        if (version >= 0x3B0B)
        {
            ar.I32(ref StateF0); ar.I32(ref StateF4); ar.I32(ref StateF8);
            ar.I32(ref StateFC); ar.I32(ref State100); ar.I32(ref State104);
        }
        if (version >= 0x3B20) ar.I32(ref State108);
        if (version >= 0x3DB9) ar.I32(ref State10C);
        if (version >= 0x3F02) ar.I32(ref State98);
    }
}

internal sealed class NativeDrawingStringArrayModel : IMfcSerializable
{
    internal string?[] Values = Array.Empty<string?>();
    public void Serialize(MfcArchive ar) => NativeDrawingDisplayArchive.Strings(ar, ref Values, true);
}

// suObList::Serialize shares CArchive count/object wire format with suObArray.
internal sealed class NativeDrawingObjectListModel : IMfcSerializable
{
    internal bool Int32Count;
    internal NativeDrawingObjectGraph.Node?[] Values = Array.Empty<NativeDrawingObjectGraph.Node?>();
    public void Serialize(MfcArchive ar)
    {
        if (!Int32Count) { NativeDrawingObjectArray.Serialize(ar, ref Values); return; }
        // moExtFeatureList 0x4C1B85C0 uses i32 from 0xF21 onward.
        if (ar.DocumentVersion < 0xF21) { NativeDrawingObjectArray.Serialize(ar, ref Values); return; }
        int count = Values.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) Values = new NativeDrawingObjectGraph.Node?[count];
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < count; i++) graph.Object(ar, ref Values[i]);
    }
}

// sldarchiveu 0x31D744D0 (suObList base, head ID, comment).
internal sealed class NativeDrawingLogsModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node?[] Stamps = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal int HeadId;
    internal string? Comment;
    public void Serialize(MfcArchive ar)
    {
        NativeDrawingObjectArray.Serialize(ar, ref Stamps);
        ar.I32(ref HeadId); DrawingPreviewRecordModel.String(ar, ref Comment);
    }
}

// sldarchiveu 0x31D74550. IDs/status are u16, time is su_CTime's u32 wire value.
internal sealed class NativeDrawingStampModel : IMfcSerializable
{
    internal ushort Author, Id, Status;
    internal uint Time;
    internal string? Comment;
    public void Serialize(MfcArchive ar)
    {
        ar.U16(ref Author); ar.U16(ref Id); ar.U16(ref Status);
        ar.U32(ref Time); DrawingPreviewRecordModel.String(ar, ref Comment);
    }
}

// sldarchiveu moSharedFileDef_c::Serialize 0x31D6E6A0; modern files
// serialize shared moCStringHandle objects rather than duplicate path strings.
internal sealed class NativeDrawingSharedFileModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? PathHandle, NameHandle;
    internal string? Path, Name, State38, State40, State50;
    internal ushort DocumentType;
    internal byte State8, State34, State35;
    internal uint Time;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 0xFA7)
        { DrawingPreviewRecordModel.String(ar, ref Path); DrawingPreviewRecordModel.String(ar, ref Name); }
        else
        {
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref PathHandle); graph.Object(ar, ref NameHandle);
        }
        ar.U16(ref DocumentType); ar.Byte(ref State8); ar.U32(ref Time);
        if (ar.DocumentVersion >= 0x1428 && ar.DocumentVersion < 0x146C) ar.Byte(ref State34);
        if (ar.DocumentVersion >= 0x1FB4) DrawingPreviewRecordModel.String(ar, ref State38);
        if (ar.DocumentVersion >= 0x23DA) DrawingPreviewRecordModel.String(ar, ref State40);
        if (ar.DocumentVersion >= 0x2F83) DrawingPreviewRecordModel.String(ar, ref State50);
        if (ar.DocumentVersion >= 0x3330) ar.Byte(ref State35);
    }
}

// sldarchiveu moExtObject_c::Serialize 0x31D6D5F0, >=0xF18 branch.
internal sealed class NativeDrawingExternalObjectModel : IMfcSerializable
{
    internal NativeDrawingSharedFileModel File = new NativeDrawingSharedFileModel();
    internal byte Flags;
    internal uint Time, SavedTime;
    internal int State48, State4C, State78, State54, State50;
    internal string? Configuration;
    internal int ConfigurationId;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 0xF18) throw new NotSupportedException("Unverified legacy external-object file record.");
        File.Serialize(ar); ar.Byte(ref Flags); ar.U32(ref Time);
        ar.I32(ref State48); ar.I32(ref State4C); ar.I32(ref State78);
        // Inline moReferencedConfiguration_c::Serialize 0x31D77DD0.
        NativeDrawingReferencedConfiguration.Serialize(ar, ref Configuration, ref ConfigurationId);
        ar.I32(ref State54);
        if (ar.DocumentVersion >= 0xFD2) ar.U32(ref SavedTime);
        if (ar.DocumentVersion >= 0x3F13) ar.I32(ref State50);
    }
}

internal sealed class NativeDrawingStringHandleModel : IMfcSerializable
{
    internal string? Value;
    public void Serialize(MfcArchive ar) => DrawingPreviewRecordModel.String(ar, ref Value);
}
