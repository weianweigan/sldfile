using System;
using System.IO;

namespace SldFile.Mfc;

// Native suObArray (0x3CB74940) delegates to su_CObArray, with a shared graph.
internal static class NativeDrawingObjectArray
{
    internal static void Serialize(MfcArchive ar, ref NativeDrawingObjectGraph.Node?[] values)
    {
        uint count = (uint)values.Length; ar.Count(ref count);
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 2) throw new InvalidDataException("Invalid native object array count.");
            values = new NativeDrawingObjectGraph.Node?[count];
        }
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < values.Length; i++) graph.Object(ar, ref values[i]);
    }
    internal sealed class Property { internal int Key; internal NativeDrawingObjectGraph.Node? Value; }
    internal static void Properties(MfcArchive ar, ref Property[] entries)
    {
        int count = entries.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) entries = new Property[count];
        for (int i = 0; i < entries.Length; i++)
        {
            if (ar.IsLoading) entries[i] = new Property();
            ar.I32(ref entries[i].Key); NativeDrawingModelFactory.Graph(ar).Object(ar, ref entries[i].Value);
        }
    }
}

// uoTempFaceTessData Serialize 0x4A03BE70; tsFaceTessellationHandle restore
// 0x31C6A2A0 delegates directly to gcFacetContainer, with no extra wire header.
internal sealed class NativeDrawingFaceTessModel : IMfcSerializable
{
    internal NativeDrawingFacetModel Facet = new NativeDrawingFacetModel();
    internal NativeDrawingObjectArray.Property[] Properties = Array.Empty<NativeDrawingObjectArray.Property>();
    internal int State34, State68, State38, EmptyName, State3C, State78;
    internal NativeDrawingFacetModel.Pair[] States = Array.Empty<NativeDrawingFacetModel.Pair>();
    internal string? Name;
    public void Serialize(MfcArchive ar)
    {
        Facet.Serialize(ar); NativeDrawingObjectArray.Properties(ar, ref Properties);
        ar.I32(ref State34); ar.I32(ref State68);
        int count = States.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) States = new NativeDrawingFacetModel.Pair[count];
        for (int i = 0; i < States.Length; i++)
        {
            if (ar.IsLoading) States[i] = new NativeDrawingFacetModel.Pair();
            ar.I32(ref States[i].Key); ar.I32(ref States[i].Value);
        }
        if (ar.DocumentVersion >= 0x1401) ar.I32(ref State38);
        if (ar.DocumentVersion >= 0x2BD0) { ar.I32(ref EmptyName); if (EmptyName == 0) DrawingPreviewRecordModel.String(ar, ref Name); }
        if (ar.DocumentVersion >= 0x336C) ar.I32(ref State3C);
        if (ar.DocumentVersion >= 0x3743) ar.I32(ref State78);
    }
}

// uoBodyPropInfo Serialize 0x4A038990.
internal sealed class NativeDrawingBodyPropertiesModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? VisualProperties;
    internal int[] States = new int[4];
    public void Serialize(MfcArchive ar)
    {
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref VisualProperties);
        for (int i = 0; i < States.Length; i++) ar.I32(ref States[i]);
    }
}

// uoTempBodyTessData Serialize 0x4A03B770.
internal sealed class NativeDrawingBodyTessModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node?[] Faces = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal int State20, State24, State2C, DecalCount, State68;
    internal NativeDrawingObjectArray.Property[] Properties = Array.Empty<NativeDrawingObjectArray.Property>();
    internal string? Name;
    public void Serialize(MfcArchive ar)
    {
        NativeDrawingObjectArray.Serialize(ar, ref Faces);
        ar.I32(ref State20); ar.I32(ref State24); ar.I32(ref State2C);
        NativeDrawingObjectArray.Properties(ar, ref Properties);
        NativeDrawingDisplayArchive.Count(ar, ref DecalCount);
        if (DecalCount != 0) throw new NotSupportedException($"Body decals are not implemented at {ar.Position}.");
        if (ar.DocumentVersion >= 0x2018) DrawingPreviewRecordModel.String(ar, ref Name);
        if (ar.DocumentVersion >= 0x2792) ar.I32(ref State68);
    }
}

// uoTempPartTessData Serialize 0x4A03C6B0, modern document layout.
internal sealed class NativeDrawingPartTessModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node?[] Bodies = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal NativeDrawingObjectArray.Property[] Properties = Array.Empty<NativeDrawingObjectArray.Property>();
    internal uint Time;
    internal int[] BodyIds = Array.Empty<int>(), BodyTypes = Array.Empty<int>();
    internal int DecalGroups, State168, State1A8, State1B0, HasGraphicsReferences;
    internal string? Name;
    internal NativeDrawingObjectGraph.Node? GraphicsReferences;
    internal double[] Bounds = new double[10];
    internal NativeDrawingReferenceGeometryModel ReferenceGeometry = new NativeDrawingReferenceGeometryModel();
    internal byte State1D8;
    public void Serialize(MfcArchive ar)
    {
        NativeDrawingObjectArray.Serialize(ar, ref Bodies);
        NativeDrawingObjectArray.Properties(ar, ref Properties);
        ar.U32(ref Time); NativeDrawingFacetModel.Ints(ar, ref BodyIds);
        if (ar.IsLoading) BodyTypes = new int[BodyIds.Length];
        for (int i = 0; i < BodyTypes.Length; i++) ar.I32(ref BodyTypes[i]);
        NativeDrawingDisplayArchive.Count(ar, ref DecalGroups);
        if (DecalGroups != 0) throw new NotSupportedException($"Part decal groups are not implemented at {ar.Position}.");
        ar.I32(ref State168); ar.I32(ref State1A8);
        if (ar.DocumentVersion == 5000) return;
        ar.I32(ref State1B0);
        DrawingPreviewRecordModel.String(ar, ref Name); ar.I32(ref HasGraphicsReferences);
        if (HasGraphicsReferences != 0) NativeDrawingModelFactory.Graph(ar).Object(ar, ref GraphicsReferences);
        for (int i = 0; i < Bounds.Length; i++) ar.F64(ref Bounds[i]);
        ReferenceGeometry.Serialize(ar);
        ar.Byte(ref State1D8);
    }
}
