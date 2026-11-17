namespace SldFile.Mfc;

// No existing native PGM profile/loop/edge serializers were found. These thin
// payloads reuse the existing CObArray codec and graph rather than rescan tags.
internal sealed class NativeSketchOffsetProfileModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node?[] Loops = System.Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal ushort State28;
    internal double Distance;
    // sgPGMProfileData_c 0x4C5B7E30. Constructor 0x4C4BBF70 establishes
    // its inline +8 array as su_CObArray with vtable 0x4CDA26C0.
    public void Serialize(MfcArchive ar)
    {
        NativeDrawingObjectArray.Serialize(ar, ref Loops); ar.U16(ref State28); ar.F64(ref Distance);
    }
}

internal sealed class NativeSketchOffsetLoopModel : IMfcSerializable
{
    internal int State8, StateC;
    internal NativeDrawingObjectGraph.Node?[] Edges = System.Array.Empty<NativeDrawingObjectGraph.Node?>();
    // sgPGMLoopOffsetData_c 0x4C5B7CE0; ctor 0x4C4BBDD0 confirms inline CObArray.
    public void Serialize(MfcArchive ar)
    {
        ar.I32(ref State8); ar.I32(ref StateC); NativeDrawingObjectArray.Serialize(ar, ref Edges);
    }
}

internal sealed class NativeSketchOffsetEdgeModel : IMfcSerializable
{
    internal ushort State8;
    internal NativeDrawingObjectGraph.Node? Objects;
    // sgPGMLoopOffsetEdgeData_c 0x4C5B7D70: u16, tagged suObArray pointer.
    public void Serialize(MfcArchive ar)
    {
        ar.U16(ref State8); NativeDrawingModelFactory.Graph(ar).Object(ar, ref Objects);
    }
}
