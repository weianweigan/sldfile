using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moModel::SerializeResolved 0x4C271810 serializes these two managers
// inline after moFiledConcept's resolved node list. They are not sketches
// or moSketchRelMgr; neither native manager had an existing codec.
internal sealed class NativeDrawingModelManagersModel : IMfcSerializable
{
    internal NativeDrawingAtomManagerModel Atoms = new NativeDrawingAtomManagerModel();
    internal NativeDrawingGhostBodyManagerModel GhostBodies = new NativeDrawingGhostBodyManagerModel();

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy model manager layout.");
        if (ar.Context.NativeDrawingArchiveConditions != 0)
            throw new NotSupportedException("Model manager export layout is not decoded.");
        Atoms.Serialize(ar); GhostBodies.Serialize(ar);
    }

    internal static void Count(MfcArchive ar, ref int count, int minimumBytes)
    {
        ar.I32(ref count);
        if (count < 0 || (ar.IsLoading && count > ar.Remaining / minimumBytes))
            throw new InvalidDataException("Invalid native model manager count.");
    }
}

// moAtomManager_c: ctor 0x4BD3C0B0, vtable 0x4D007410,
// Serialize 0x4BD3DF20. Preserve wire order of the pointer-map entries.
internal sealed class NativeDrawingAtomManagerModel : IMfcSerializable
{
    internal int State260;
    internal uint StateA8;
    internal NativeDrawingObjectGraph.Node?[] Atoms = Array.Empty<NativeDrawingObjectGraph.Node?>();

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy atom manager layout.");
        ar.I32(ref State260);
        int count = Atoms.Length; NativeDrawingModelManagersModel.Count(ar, ref count, 2);
        if (ar.IsLoading) Atoms = new NativeDrawingObjectGraph.Node?[count];
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < count; i++) graph.Object(ar, ref Atoms[i]);
        ar.U32(ref StateA8);
    }
}

// moGhostBodyIdManager_c: ctor 0x4BF4CDD0, vtable 0x4D0AB7A8,
// Serialize 0x4BF4E420. High four bits of the count are persistent flags.
internal sealed class NativeDrawingGhostBodyManagerModel : IMfcSerializable
{
    internal int State80;
    internal uint Flags;
    internal Body[] Bodies = Array.Empty<Body>();

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy ghost body manager layout.");
        if (Flags > 15 || Bodies.Length > 0x0FFFFFFF)
            throw new InvalidDataException("Invalid packed ghost body count/flags.");
        ar.I32(ref State80);
        uint packed = (Flags << 28) | (uint)Bodies.Length; ar.U32(ref packed);
        int count = (int)(packed & 0x0FFFFFFF);
        if (ar.IsLoading)
        {
            Flags = packed >> 28;
            if (count > ar.Remaining / 28) throw new InvalidDataException("Invalid ghost body count.");
            Bodies = new Body[count];
        }
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) Bodies[i] = new Body();
            Bodies[i].Serialize(ar);
        }
    }

    // Inline moGhostBodyObject_c, vtable 0x4D0AB718 -> 0x4BF4E6D0.
    internal sealed class Body
    {
        internal int State8, StateC, State10, StateC0, HasGeometry;
        internal uint[] Ids = Array.Empty<uint>();
        internal NativeDrawingObjectGraph.Node? Geometry;
        internal DrawingPreviewPoint[] Points = Array.Empty<DrawingPreviewPoint>();

        internal void Serialize(MfcArchive ar)
        {
            ar.I32(ref State8); ar.I32(ref StateC); ar.I32(ref State10); ar.I32(ref StateC0);
            int count = Ids.Length; NativeDrawingModelManagersModel.Count(ar, ref count, 4);
            if (ar.IsLoading) Ids = new uint[count];
            for (int i = 0; i < count; i++) ar.U32(ref Ids[i]);
            ar.I32(ref HasGeometry);
            if (HasGeometry != 0) NativeDrawingModelFactory.Graph(ar).Object(ar, ref Geometry);
            count = Points.Length; NativeDrawingModelManagersModel.Count(ar, ref count, 24);
            if (ar.IsLoading) Points = new DrawingPreviewPoint[count];
            // mgPoint::save/restore (sldmgu 0x3216F260 / 0x3216E750)
            // use three ordinary archive doubles, matching the shared point codec.
            for (int i = 0; i < count; i++) DrawingPreviewRecordModel.Point(ar, ref Points[i]);
        }
    }
}
