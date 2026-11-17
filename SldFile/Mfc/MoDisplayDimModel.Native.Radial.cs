using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

public abstract partial class MoDisplayDimModel
{
    // moDisplayRadialDim factory 0x4C8471B0 -> ctor 0x4C8E0CC0,
    // vtable 0x4D264FD0 -> Serialize 0x4C853D90. This is only the
    // derived tail; the common display dimension has one shared codec.
    internal sealed class NativeRadialState
    {
        internal int StateFD8, HasTransform1060, StateFD4, State10A4;
        internal double[] ValuesFE0 = new double[6];
        internal DrawingPreviewPoint Point1010, Point1028;
        internal string?[] CalloutStrings = Array.Empty<string?>();
        internal string? State1048;
        internal NativeDrawingObjectGraph.Node? Callout1050, Related1068, Mirror1090, HoleWizard10A8;
        internal NativeDrawingTransformModel Transform1060 = new NativeDrawingTransformModel();
        internal void Serialize(MfcArchive ar)
        {
            if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy radial display layout.");
            ar.I32(ref StateFD8);
            if (ValuesFE0.Length != 6) throw new InvalidDataException("Six radial display values are required.");
            for (int i = 0; i < 6; i++) ar.F64(ref ValuesFE0[i]);
            DrawingPreviewRecordModel.Point(ar, ref Point1010); DrawingPreviewRecordModel.Point(ar, ref Point1028);
            int count = CalloutStrings.Length; ar.I32(ref count);
            // The native reader allocates four CString slots. Preserve
            // the actual filed count instead of normalizing it on load.
            if (count < 0 || count > 4) throw new InvalidDataException("Invalid radial callout string count.");
            if (ar.IsLoading) CalloutStrings = new string?[count];
            for (int i = 0; i < count; i++) DrawingPreviewRecordModel.String(ar, ref CalloutStrings[i]);
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref Callout1050); DrawingPreviewRecordModel.String(ar, ref State1048);
            graph.Object(ar, ref Related1068); ar.I32(ref HasTransform1060);
            if (HasTransform1060 != 0) Transform1060.Serialize(ar);
            graph.Object(ar, ref Mirror1090);
            if (ar.DocumentVersion > 0x23AA) ar.I32(ref StateFD4);
            if (ar.DocumentVersion > 0x333D) graph.Object(ar, ref HoleWizard10A8);
            if (ar.DocumentVersion > 0x3B21) ar.I32(ref State10A4);
        }
    }
}
