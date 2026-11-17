using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moDetailCircle factory 0x4C963E60 -> ctor 0x4BAA54B0, vtable
// 0x4CF0F828 -> Serialize 0x4C96E760. LW/resolved virtuals are
// moConcept, so reuse the ordinary moFeature codec. The inline +350
// object is the existing moDimText, not a second text implementation.
internal sealed class NativeDrawingDetailCircleModel : IMfcSerializable
{
    internal NativeDrawingFeatureModel Feature = new NativeDrawingFeatureModel();
    internal NativeDrawingDimTextModel Text = new NativeDrawingDimTextModel();
    internal NativeDrawingObjectGraph.Node? Sketch;
    internal NativeDrawingObjectGraph.Node?[] Handles = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal DrawingPreviewPoint Point4C0, Point4B8, Vector4D8;
    internal int State4C8, State4CC, State4F8, State4FC, State500;
    internal byte State4D0;
    internal double State548;
    internal int[] States550 = new int[5];
    public void Serialize(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        if (version < 5000) throw new NotSupportedException("Unverified legacy detail-circle layout.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Detail-circle export layout is not decoded.");
        Feature.Serialize(ar); Text.Serialize(ar);
        var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref Sketch);
        if (Handles.Length > ushort.MaxValue) throw new InvalidDataException("Too many detail-circle sketch handles.");
        ushort count = (ushort)Handles.Length; ar.U16(ref count);
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 2) throw new InvalidDataException("Detail-circle sketch handles exceed the archive.");
            Handles = new NativeDrawingObjectGraph.Node?[count];
        }
        for (int i = 0; i < count; i++) graph.Object(ar, ref Handles[i]);
        DrawingPreviewRecordModel.Point(ar, ref Point4C0); DrawingPreviewRecordModel.Point(ar, ref Point4B8);
        ar.I32(ref State4C8); ar.I32(ref State4CC); ar.I32(ref State4F8);
        DrawingPreviewRecordModel.Point(ar, ref Vector4D8); ar.Byte(ref State4D0);
        if (version > 0x23AD) { ar.I32(ref State4FC); ar.F64(ref State548); }
        if (version > 0x23BE) ar.I32(ref State500);
        if (version >= 0x23C9)
        {
            if (States550.Length != 5) throw new InvalidDataException("Five detail-circle states are required.");
            for (int i = 0; i < 5; i++) ar.I32(ref States550[i]);
        }
    }
}
