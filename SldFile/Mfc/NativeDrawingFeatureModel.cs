using System;

namespace SldFile.Mfc;

// moEntVisProp::Serialize 0x4C1915B0 -> moVisProp::Serialize 0x4C1916B0.
internal sealed class NativeDrawingEntityVisualModel : IMfcSerializable
{
    internal ushort State0, State2, StateC;
    internal byte State4;
    internal uint Color;
    internal NativeDrawingLineWidthModel Width = new NativeDrawingLineWidthModel { RestoreType = 2 };
    public void Serialize(MfcArchive ar)
    {
        ar.U16(ref State0); ar.U16(ref State2);
        if (ar.DocumentVersion >= 0x447) ar.Byte(ref State4);
        ar.U32(ref Color); ar.U16(ref StateC); Width.Serialize(ar);
    }
}

// moFeature::Serialize 0x4BB61230, ordinary drawing Definition context
// (no DB keys, pointer export or process-encryption noise).
internal sealed class NativeDrawingFeatureModel : IMfcSerializable
{
    internal NativeDrawingNodeLightweightModel Lightweight = new NativeDrawingNodeLightweightModel();
    internal NativeDrawingNodeListModel ResolvedChildren = new NativeDrawingNodeListModel();
    internal int State290, State284, State2A0, State2B0, State2B4, State2A8, State2AC;
    internal uint State294;
    internal byte HasString, State1EC, State2F8, State2F9;
    internal string? OptionalString, State1F0;
    internal double State1D8;
    internal NativeDrawingObjectGraph.Node? Folder, Comment;
    internal NativeDrawingEntityVisualModel Visual = new NativeDrawingEntityVisualModel();
    internal int State2E4, StateA0, State310, HasSubFeature, State304, State2A4, State33C;
    internal uint State200, TimeHigh, TimeLow;
    internal ushort State90;

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy drawing feature layout.");
        var graph = NativeDrawingModelFactory.Graph(ar);
        // The native folder/view/plane feature vtables use moConcept's
        // SerializeLWData and SerializeResolvedData at +0x4A8/+0x4B0.
        Lightweight.Serialize(ar); ResolvedChildren.Serialize(ar);
        ar.I32(ref State290); ar.I32(ref State284); ar.U32(ref State294);
        ar.Byte(ref HasString);
        if (HasString != 0) DrawingPreviewRecordModel.String(ar, ref OptionalString);
        ar.I32(ref State2A0); ar.I32(ref State2B0); ar.I32(ref State2B4);
        ar.I32(ref State2A8); ar.F64(ref State1D8); ar.I32(ref State2AC);
        graph.Object(ar, ref Folder); DrawingPreviewRecordModel.String(ar, ref State1F0);
        ar.Byte(ref State1EC); Visual.Serialize(ar);
        ar.I32(ref State2E4); ar.I32(ref StateA0); graph.Object(ar, ref Comment);
        ar.U32(ref State200); ar.Byte(ref State2F8); ar.Byte(ref State2F9);
        ar.I32(ref State310); ar.I32(ref HasSubFeature); ar.I32(ref State304);
        ar.U32(ref TimeHigh); ar.U32(ref TimeLow); ar.U16(ref State90);
        if (ar.DocumentVersion >= 0x23FB) ar.I32(ref State2A4);
        if (ar.DocumentVersion >= 0x279C) ar.I32(ref State33C);
        // Feature SerializeXmlData updates external XML state only; no bytes.
    }
}

// moFolder::Serialize 0x4BEFC610, shared by the native folder subclasses.
internal sealed class NativeDrawingFolderModel : IMfcSerializable
{
    internal NativeDrawingFeatureModel Feature = new NativeDrawingFeatureModel();
    internal int State358, State368;
    public void Serialize(MfcArchive ar)
    {
        Feature.Serialize(ar);
        if (ar.DocumentVersion >= 0xA93) ar.I32(ref State358);
        if (ar.DocumentVersion >= 0x27B4) ar.I32(ref State368);
    }
}

// moModelFeature::Serialize 0x4BB68A30. Its compatibility helper
// 0x4BB7A990 emits no bytes outside versions 0x393..0x3AE.
internal sealed class NativeDrawingModelFeatureModel : IMfcSerializable
{
    internal NativeDrawingFeatureModel Feature = new NativeDrawingFeatureModel();
    internal int State354;
    public void Serialize(MfcArchive ar)
    {
        Feature.Serialize(ar);
        if (ar.DocumentVersion >= 0xDB3) ar.I32(ref State354);
    }
}

// moDetailCabinet::Serialize 0x4BA9AB60. Constructor 0x4BA78110
// establishes both inline containers as suObArray, not tagged objects.
internal sealed class NativeDrawingDetailCabinetModel : IMfcSerializable
{
    internal NativeDrawingModelFeatureModel Feature = new NativeDrawingModelFeatureModel();
    internal NativeDrawingObjectGraph.Node?[] Objects = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal double ScaleX, ScaleY;
    internal NativeDrawingObjectGraph.Node?[] Details = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal NativeDrawingObjectGraph.Node?[] ImportedAnnotations = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal int State3E8;
    public void Serialize(MfcArchive ar)
    {
        Feature.Serialize(ar); NativeDrawingObjectArray.Serialize(ar, ref Objects);
        ar.F64(ref ScaleX); ar.F64(ref ScaleY); NativeDrawingObjectArray.Serialize(ar, ref Details);
        if (ar.DocumentVersion >= 0x27B9)
        {
            int count = ImportedAnnotations.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) ImportedAnnotations = new NativeDrawingObjectGraph.Node?[count];
            var graph = NativeDrawingModelFactory.Graph(ar);
            for (int i = 0; i < count; i++) graph.Object(ar, ref ImportedAnnotations[i]);
        }
        if (ar.DocumentVersion >= 0x2B9B) ar.I32(ref State3E8);
    }
}
