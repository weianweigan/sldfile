using System;
using SldFile.Binary;

namespace SldFile.Mfc;

// moRefWrapper 0x4C2B46F0, ordinary >=5000 layout. Shared by circle
// reference wrappers and existing moSketchExtRef_w records.
internal class NativeDrawingReferenceWrapperModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? Target50, Sketch, Owner58;
    internal ushort Kind60, HasTransform10, HasTransform18;
    internal int State64, State8, StateC, State20, State6C, State70, State74, State4C, State68, State34, State30, State90, State38;
    internal NativeDrawingTransformModel Transform10 = new NativeDrawingTransformModel(), Transform18 = new NativeDrawingTransformModel();

    public virtual void Serialize(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        if (version < 5000) throw new NotSupportedException("Unverified legacy reference-wrapper layout.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Reference-wrapper export layout is not decoded.");
        bool includesOwnerProfile = ar.Context.NativeSketchIncludesOwnerProfile
            ?? throw new InvalidOperationException("Native reference wrapper requires explicit archive +834 context.");
        if (includesOwnerProfile)
            throw new NotSupportedException("Reference-wrapper archive +834 layout is not decoded.");
        var graph = NativeDrawingModelFactory.Graph(ar);
        graph.Object(ar, ref Target50);
        if (Target50?.Payload is EntHandleData) graph.Object(ar, ref Sketch);
        ar.U16(ref Kind60); graph.Object(ar, ref Owner58);
        ar.I32(ref State64); ar.I32(ref State8);
        ar.U16(ref HasTransform10); if (HasTransform10 != 0) Transform10.Serialize(ar);
        ar.U16(ref HasTransform18); if (HasTransform18 != 0) Transform18.Serialize(ar);
        ar.I32(ref StateC); ar.I32(ref State20); ar.I32(ref State6C); ar.I32(ref State70);
        if (version > 0x2FAF) ar.I32(ref State74);
        ar.I32(ref State4C); ar.I32(ref State68);
        if (version > 0x1FD1) { ar.I32(ref State34); ar.I32(ref State30); }
        if (version >= 0x2B73) ar.I32(ref State90);
        if (version >= 0x3B02) ar.I32(ref State38);
    }
}
