using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class ExtEntData : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? NativeReference, NativeView, NativeIntersection, NativeIsoCurve;
    internal string? NativeName;

    // sgExtEnt factory 0x4C5A1050, Serialize 0x4C5B0FB0. Ordinary
    // >=5000 layout: reference wrapper, view, intersection/iso information,
    // then CString. The much older entity-to-wrapper migrations are excluded.
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy external sketch entity.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("External sketch-entity export layout is not decoded.");
        var graph = NativeDrawingModelFactory.Graph(ar);
        graph.Object(ar, ref NativeReference); graph.Object(ar, ref NativeView);
        graph.Object(ar, ref NativeIntersection); graph.Object(ar, ref NativeIsoCurve);
        DrawingPreviewRecordModel.String(ar, ref NativeName);
    }
}
