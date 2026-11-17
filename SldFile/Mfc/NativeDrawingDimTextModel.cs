using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// sldmodu moDimText_c::Serialize 0x4C70E290, ordinary non-DB archive.
// Existing SketchSegmentModel represents text geometry; it is not this
// independently referenced native object with font and cached glyph topology.
internal sealed class NativeDrawingDimTextModel : IMfcSerializable
{
    internal bool Generic;
    internal NativeDrawingObjectGraph.Node? GenericData, DisplayItem;
    internal int GenericState160;
    internal NativeDrawingParagraphFormat Paragraph = new NativeDrawingParagraphFormat();
    internal string? Text;
    internal DrawingPreviewPoint Position, SecondaryPosition;
    internal int State10, State20, State24, StateC4, StateA0, StateC0, StateD0;
    internal int State118, State120, State124, State130;
    internal double State68, StateD8, StateE0, State128;
    internal NativeDrawingObjectGraph.Node? Format;
    internal int[] GroupSizes = Array.Empty<int>(), CurveTypes = Array.Empty<int>(), PointCounts = Array.Empty<int>();
    internal DrawingPreviewPoint[] GlyphPoints = Array.Empty<DrawingPreviewPoint>();
    internal DrawingPreviewPoint[] PathPoints = Array.Empty<DrawingPreviewPoint>(), PathVectors = Array.Empty<DrawingPreviewPoint>();
    internal int[] PathStates = Array.Empty<int>();
    internal NativeDrawingStringArrayModel Strings = new NativeDrawingStringArrayModel();

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native dimension-text layout.");
        DrawingPreviewRecordModel.String(ar, ref Text); DrawingPreviewRecordModel.Point(ar, ref Position);
        ar.I32(ref State10); NativeDrawingModelFactory.Graph(ar).Object(ar, ref Format); ar.I32(ref State20);
        DrawingPreviewRecordModel.Point(ar, ref SecondaryPosition); ar.F64(ref State68);
        int count = GroupSizes.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        Ints(ar, ref GroupSizes, count);
        int curves = Sum(GroupSizes); Ints(ar, ref CurveTypes, curves); Ints(ar, ref PointCounts, curves);
        Points(ar, ref GlyphPoints, Sum(PointCounts));
        ar.I32(ref StateC4); ar.I32(ref StateA0);
        if (StateA0 != 0)
        {
            count = PathPoints.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            Points(ar, ref PathPoints, count); Ints(ar, ref PathStates, count); Points(ar, ref PathVectors, count);
            ar.I32(ref StateC0);
        }
        ar.I32(ref StateD0); ar.F64(ref StateD8); ar.F64(ref StateE0); ar.I32(ref State24);
        Strings.Serialize(ar); ar.I32(ref State118); ar.I32(ref State120);
        ar.I32(ref State124); ar.F64(ref State128); ar.I32(ref State130);
        if (Generic)
        {
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref GenericData); ar.I32(ref GenericState160); graph.Object(ar, ref DisplayItem); Paragraph.Serialize(ar);
        }
    }

    private static int Sum(int[] values)
    {
        int count = 0;
        foreach (int value in values)
        {
            if (value < 0) throw new InvalidDataException("Negative native glyph topology count.");
            count = checked(count + value);
        }
        return count;
    }

    private static void Ints(MfcArchive ar, ref int[] values, int count)
    {
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 4) throw new InvalidDataException("Invalid native text integer count.");
            values = new int[count];
        }
        else if (values.Length != count) throw new InvalidDataException("Inconsistent native text integer topology.");
        for (int i = 0; i < count; i++) ar.I32(ref values[i]);
    }

    private static void Points(MfcArchive ar, ref DrawingPreviewPoint[] values, int count)
    {
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 24) throw new InvalidDataException("Invalid native text point count.");
            values = new DrawingPreviewPoint[count];
        }
        else if (values.Length != count) throw new InvalidDataException("Inconsistent native text point topology.");
        for (int i = 0; i < count; i++) DrawingPreviewRecordModel.Point(ar, ref values[i]);
    }
}

// sldmfcu utCharFormat_c::Serialize 0x3CA681A0. Font substitution and
// registry/UI side effects in the native loader do not change the wire layout.
internal sealed class NativeDrawingCharacterFormatModel : IMfcSerializable
{
    internal string? FontName;
    internal double State10, State20, State28, State38, State40, State48, State50;
    internal int State18;
    internal uint State30, State58;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native character format.");
        DrawingPreviewRecordModel.String(ar, ref FontName); ar.F64(ref State10); ar.I32(ref State18);
        ar.F64(ref State20); ar.U32(ref State30); ar.F64(ref State28);
        ar.F64(ref State38); ar.F64(ref State40); ar.F64(ref State48); ar.F64(ref State50);
        if (ar.DocumentVersion >= 0x3EED) ar.U32(ref State58);
    }
}
