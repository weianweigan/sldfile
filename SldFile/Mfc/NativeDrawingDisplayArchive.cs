using System;
using System.IO;

namespace SldFile.Mfc;

// slddrwu.dll uiDrawingDisplayListObject_c::Serialize 0x3E0D6740 and
// uiDrawingSheetBucket_c::Serialize 0x3E0D8030. All offsets are consumed
// sequentially from native counts and version gates, never found by scanning.
internal sealed class NativeDrawingDisplayArchive : IMfcSerializable
{
    internal sealed class Exclusion
    {
        internal string? Document;
        internal int WholeDocument;
        internal string?[] Views = Array.Empty<string?>();
    }

    internal int DocumentVersion;
    internal int Format = 1;
    internal NativeDrawingTransformModel ViewTransform = new NativeDrawingTransformModel();
    internal NativeDrawingTransformModel PanZoomTransform = new NativeDrawingTransformModel();
    internal Exclusion[] Exclusions = Array.Empty<Exclusion>();
    internal string?[] ExcludedFiles = Array.Empty<string?>();
    internal NativeDrawingObjectGraph.Node?[] LineStyles = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal NativeDrawingObjectGraph.Node? LineFonts, Layers;
    internal int MetadataEnd;
    internal int[] ViewIndices = Array.Empty<int>();
    internal NativeDrawingFormatBucketModel[] Formats = Array.Empty<NativeDrawingFormatBucketModel>();
    internal NativeDrawingDisplayViewModel[] Views = Array.Empty<NativeDrawingDisplayViewModel>();
    internal int DetailMask47, DetailMask51, Reserved;
    internal int RootEnd;
    internal NativeDrawingDisplayTailModel Tail = new NativeDrawingDisplayTailModel();

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsLoading) DocumentVersion = ar.DocumentVersion;
        else ar.DocumentVersion = DocumentVersion;
        SerializeRoot(ar);
        RootEnd = ar.Position;
        if (ar.Context.NativeDrawingObjects == null) ar.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        Tail.Serialize(ar);
    }

    private void SerializeRoot(MfcArchive ar)
    {
        if (DocumentVersion < 0x309) throw new NotSupportedException("Unverified legacy drawing display envelope.");
        ar.I32(ref Format);
        if (Format == 0) return;
        ViewTransform.Serialize(ar); PanZoomTransform.Serialize(ar);
        var graph = NativeDrawingModelFactory.CreateGraph();
        ar.Context.NativeDrawingObjects = graph;
        if (DocumentVersion >= 0x1433)
        {
            int count = Exclusions.Length; Count(ar, ref count);
            if (ar.IsLoading) Exclusions = new Exclusion[count];
            for (int i = 0; i < Exclusions.Length; i++)
            {
                if (ar.IsLoading) Exclusions[i] = new Exclusion();
                var item = Exclusions[i];
                DrawingPreviewRecordModel.String(ar, ref item.Document);
                ar.I32(ref item.WholeDocument);
                if (item.WholeDocument == 0) Strings(ar, ref item.Views, false);
            }
        }
        if (DocumentVersion >= 0x182F) Strings(ar, ref ExcludedFiles, true);
        if (DocumentVersion >= 0xA30)
        {
            int count = LineStyles.Length; Count(ar, ref count);
            if (ar.IsLoading) LineStyles = new NativeDrawingObjectGraph.Node?[count];
            for (int i = 0; i < LineStyles.Length; i++) graph.Object(ar, ref LineStyles[i]);
        }
        if (DocumentVersion >= 0x2FF) graph.Object(ar, ref LineFonts);
        if (DocumentVersion >= 0xC58) graph.Object(ar, ref Layers);
        MetadataEnd = ar.Position;
        int indices = ViewIndices.Length; Count(ar, ref indices);
        if (ar.IsLoading) ViewIndices = new int[indices];
        for (int i = 0; i < ViewIndices.Length; i++) ar.I32(ref ViewIndices[i]);
        int formats = Formats.Length; Count(ar, ref formats);
        if (ar.IsLoading) Formats = new NativeDrawingFormatBucketModel[formats];
        for (int i = 0; i < Formats.Length; i++)
        {
            if (ar.IsLoading) Formats[i] = new NativeDrawingFormatBucketModel();
            Formats[i].Serialize(ar);
        }
        int views = Views.Length; Count(ar, ref views);
        if (ar.IsLoading) Views = new NativeDrawingDisplayViewModel[views];
        for (int i = 0; i < Views.Length; i++)
        {
            if (ar.IsLoading) Views[i] = new NativeDrawingDisplayViewModel();
            Views[i].Serialize(ar);
        }
        if (DocumentVersion >= 0x11FD) ar.I32(ref DetailMask47);
        if (DocumentVersion >= 0x12EE) ar.I32(ref DetailMask51);
        if (DocumentVersion >= 0x1425) ar.I32(ref Reserved);
    }

    internal static void Count(MfcArchive ar, ref int count)
    {
        ar.I32(ref count);
        if (count < 0 || ar.IsLoading && count > ar.Remaining / 2)
            throw new InvalidDataException("Invalid native drawing collection count.");
    }

    internal static void Strings(MfcArchive ar, ref string?[] values, bool archiveCount)
    {
        int count = values.Length;
        if (archiveCount)
        {
            uint native = (uint)count; ar.Count(ref native); count = checked((int)native);
            if (ar.IsLoading && count > ar.Remaining / 4) throw new InvalidDataException("Invalid native CString array count.");
        }
        else Count(ar, ref count);
        if (ar.IsLoading) values = new string?[count];
        for (int i = 0; i < values.Length; i++) DrawingPreviewRecordModel.String(ar, ref values[i]);
    }
}
