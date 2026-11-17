using System;
using SldFile.Mfc;

namespace SldFile.Binary;

// Shared moCompRef_c wire state, also inherited by component annotations.
internal abstract class NativeComponentReferenceState
{
    internal NativeDrawingObjectGraph.Node? NativeOwner, NativeFacet;
    internal int NativeState68, NativeState18, NativeState1C, NativeState20, NativeState30, NativeState70;
    internal int NativeHas84, NativeStateA0, NativeState48, NativeHasFacet, NativeHasName, NativeHasInfo, NativeHasTransform, NativeHasPath;
    internal uint NativeState34, NativeState4C;
    internal int[] NativeStates90 = new int[4];
    internal string? NativeName, NativePath;
    internal NativeDrawingTransformModel NativeTransform = new NativeDrawingTransformModel();

    // vtable 0x4CDA2F08 -> 0x4BBF6220 -> 0x4BBF60D0 ->
    // 0x4BBF56F0 -> moCompRef::Serialize 0x4BBF6320.
    internal void SerializeReferenceBase(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        if (version < 5000) throw new NotSupportedException("Unverified legacy component-plane reference.");
        var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref NativeOwner);
        ar.I32(ref NativeState68); ar.I32(ref NativeState18); ar.I32(ref NativeState1C);
        ar.I32(ref NativeState20); ar.I32(ref NativeState30); ar.I32(ref NativeState70);
        ar.I32(ref NativeHas84);
        if (NativeHas84 != 0) throw new NotSupportedException("Native component reference inline +84 data is not decoded.");
        ar.I32(ref NativeStateA0); ar.U32(ref NativeState34); ar.I32(ref NativeState48);
        if (version > 0x279F && version < 0x27A8) ar.I32(ref NativeStates90[1]);
        if (version > 0x27A7)
        {
            if (NativeStates90.Length != 4) throw new System.IO.InvalidDataException("Four component-reference states are required.");
            for (int i = 0; i < 4; i++) ar.I32(ref NativeStates90[i]);
        }
        if (version > 0x27B1)
        {
            ar.I32(ref NativeHasFacet); if (NativeHasFacet != 0) graph.Object(ar, ref NativeFacet);
        }
        if (version > 0x27CA && version < 0x27CC) DrawingPreviewRecordModel.String(ar, ref NativeName);
        if (version > 0x27CB)
        {
            ar.I32(ref NativeHasName); if (NativeHasName != 0) DrawingPreviewRecordModel.String(ar, ref NativeName);
        }
        if (version > 0x2B60)
        {
            ar.I32(ref NativeHasInfo);
            if (NativeHasInfo != 0) throw new NotSupportedException("Native moCompRefInfo_c is not decoded.");
        }
        if (version > 0x2BB8)
        {
            ar.I32(ref NativeHasTransform); if (NativeHasTransform != 0) NativeTransform.Serialize(ar);
        }
        if (version > 0x2F55)
        {
            ar.I32(ref NativeHasPath); if (NativeHasPath != 0) DrawingPreviewRecordModel.String(ar, ref NativePath);
        }
        if (version >= 0x3EF6) ar.U32(ref NativeState4C);
    }
}

internal sealed partial class CompRefPlaneData : NativeDrawingComponentFeatureReferenceModel, IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? NativeGeometry;
    internal int NativeState100;
    public override void Serialize(MfcArchive ar)
    {
        base.Serialize(ar);
        // Ordinary condition-1=false branch calls moFRData::Serialize
        // 0x4BB842A0 inline: i32 feature index and su_CTime's U32.
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref NativeGeometry); ar.I32(ref NativeState100);
    }
}
