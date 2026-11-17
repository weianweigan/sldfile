using System;

namespace SldFile.Mfc;

// Shared geometry-feature payload 0x4BB5A850, normal Definition context.
// Pointer-export, DB and partition-only archive modes are separate protocols.
internal sealed class NativeDrawingGeometryFeatureModel : IMfcSerializable
{
    internal sealed class OptionalArray : IMfcSerializable
    {
        internal int Present;
        internal NativeDrawingObjectGraph.Node?[] Values = Array.Empty<NativeDrawingObjectGraph.Node?>();
        // suObArray::serializePtr 0x3CB77330: i32 presence, inline suObArray.
        public void Serialize(MfcArchive ar)
        {
            ar.I32(ref Present);
            if (Present != 0) NativeDrawingObjectArray.Serialize(ar, ref Values);
            else if (ar.IsLoading) Values = Array.Empty<NativeDrawingObjectGraph.Node?>();
        }
    }
    internal sealed class ObjectNumber { internal NativeDrawingObjectGraph.Node? Object; internal uint Number; }
    internal sealed class ObjectText { internal NativeDrawingObjectGraph.Node? Object; internal string? Text; }
    internal sealed class NumberText { internal int Number; internal string? Text; }
    internal NativeDrawingModelFeatureModel Feature = new NativeDrawingModelFeatureModel();
    internal NativeDrawingObjectGraph.Node?[] Objects360 = Array.Empty<NativeDrawingObjectGraph.Node?>(), Objects380 = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal ushort HasIdKeeper, HasBodyChooser4A0, State498, HasBodyChooser4A8, HasBodyChooser4B8, HasBodyChooser4C0;
    internal NativeDrawingObjectGraph.Node? IdKeeper, Atom, PartitionMark;
    internal OptionalArray Objects520 = new OptionalArray(), Objects528 = new OptionalArray(), Objects540 = new OptionalArray(), Objects548 = new OptionalArray();
    internal int[] Ids = Array.Empty<int>();
    internal int State538, State53C, State4B0, State570, HasPartitionMark, Partition3B8, Partition3C0, Partition578, State5C8, State488, HasData6A0, State6B4;
    internal NativeDrawingFacetModel.Pair[] Map670 = Array.Empty<NativeDrawingFacetModel.Pair>(), Map3F8 = Array.Empty<NativeDrawingFacetModel.Pair>();
    internal ObjectNumber[] EdgeNumbers = Array.Empty<ObjectNumber>();
    internal NativeDrawingObjectArray.Property[] VisualProperties = Array.Empty<NativeDrawingObjectArray.Property>();
    internal ObjectText[] FaceNames = Array.Empty<ObjectText>();
    internal NumberText[] Names = Array.Empty<NumberText>();

    public void Serialize(MfcArchive ar)
    {
        Feature.Serialize(ar);
        var graph = NativeDrawingModelFactory.Graph(ar);
        int version = ar.DocumentVersion;
        NativeDrawingObjectArray.Serialize(ar, ref Objects360); NativeDrawingObjectArray.Serialize(ar, ref Objects380);
        ar.U16(ref HasIdKeeper); if (HasIdKeeper != 0) graph.Object(ar, ref IdKeeper);
        Objects520.Serialize(ar); Objects528.Serialize(ar); graph.Object(ar, ref Atom);
        NativeDrawingFacetModel.Ints(ar, ref Ids); ar.I32(ref State538);
        Objects540.Serialize(ar); ar.I32(ref State53C);
        ar.U16(ref HasBodyChooser4A0); RequireAbsent(HasBodyChooser4A0, "body chooser 4A0");
        ar.U16(ref State498); ar.U16(ref HasBodyChooser4A8); RequireAbsent(HasBodyChooser4A8, "body chooser 4A8");
        if (version >= 0x10F9)
        {
            ar.I32(ref State4B0); ar.U16(ref HasBodyChooser4B8); RequireAbsent(HasBodyChooser4B8, "body chooser 4B8");
            ar.U16(ref HasBodyChooser4C0); RequireAbsent(HasBodyChooser4C0, "body chooser 4C0");
        }
        ar.I32(ref State570); Objects548.Serialize(ar);
        // sldmfcu hasCondition(0), 0x3CA54B60, tests the entire mask
        // for zero. moDrView scopes 0x40, so its nested geometry omits
        // this block even in an ordinary file archive.
        uint conditions = ar.Context.NativeDrawingArchiveConditions;
        if (conditions == 0 || (conditions & 0x400000) != 0)
        {
            ar.I32(ref HasPartitionMark);
            if (HasPartitionMark != 0)
            {
                graph.Object(ar, ref PartitionMark); ar.I32(ref Partition3B8); ar.I32(ref Partition3C0); ar.I32(ref Partition578);
            }
        }
        ar.I32(ref State5C8); Pairs(ar, ref Map670);
        int edges = EdgeNumbers.Length; NativeDrawingDisplayArchive.Count(ar, ref edges);
        if (ar.IsLoading) EdgeNumbers = new ObjectNumber[edges];
        for (int i = 0; i < edges; i++)
        {
            if (ar.IsLoading) EdgeNumbers[i] = new ObjectNumber();
            graph.Object(ar, ref EdgeNumbers[i].Object); ar.U32(ref EdgeNumbers[i].Number);
        }
        Pairs(ar, ref Map3F8); ar.I32(ref State488);
        NativeDrawingObjectArray.Properties(ar, ref VisualProperties);
        ar.I32(ref HasData6A0); RequireAbsent(HasData6A0, "geometry-feature data 6A0");
        int faces = FaceNames.Length; NativeDrawingDisplayArchive.Count(ar, ref faces);
        if (ar.IsLoading) FaceNames = new ObjectText[faces];
        for (int i = 0; i < faces; i++)
        {
            if (ar.IsLoading) FaceNames[i] = new ObjectText();
            graph.Object(ar, ref FaceNames[i].Object); DrawingPreviewRecordModel.String(ar, ref FaceNames[i].Text);
        }
        int names = Names.Length; NativeDrawingDisplayArchive.Count(ar, ref names);
        if (ar.IsLoading) Names = new NumberText[names];
        for (int i = 0; i < names; i++)
        {
            if (ar.IsLoading) Names[i] = new NumberText();
            ar.I32(ref Names[i].Number); DrawingPreviewRecordModel.String(ar, ref Names[i].Text);
        }
        if (version >= 0x17E4) ar.I32(ref State6B4);
    }

    private static void Pairs(MfcArchive ar, ref NativeDrawingFacetModel.Pair[] values)
    {
        int count = values.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) values = new NativeDrawingFacetModel.Pair[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) values[i] = new NativeDrawingFacetModel.Pair();
            ar.I32(ref values[i].Key); ar.I32(ref values[i].Value);
        }
    }
    private static void RequireAbsent(int present, string name)
    {
        if (present != 0) throw new NotSupportedException($"Native drawing {name} is not decoded.");
    }
}
