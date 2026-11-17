using System;

namespace SldFile.Mfc;

internal sealed partial class DrDimensionMfcModel
{
    // moDimBackupDef descriptor 0x4DB03AF0, CreateObject 0x4C8726E0
    // -> vtable 0x4D255D60 -> Serialize 0x4C872B80. This is a dimension
    // snapshot, distinct from moDisplayDistanceDim's visual payload.
    internal sealed class NativeBackupState
    {
        internal int State20, State58;
        internal double Value28;
        internal NativeDrawingObjectGraph.Node? Tolerance;
        internal string? State38, State40, State48, State50, LastString;
        internal void Serialize(MfcArchive ar)
        {
            if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
                throw new NotSupportedException("Dimension-backup export layout is not decoded.");
            ar.I32(ref State20); ar.F64(ref Value28);
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref Tolerance);
            DrawingPreviewRecordModel.String(ar, ref State38); DrawingPreviewRecordModel.String(ar, ref State40);
            DrawingPreviewRecordModel.String(ar, ref State48); DrawingPreviewRecordModel.String(ar, ref State50);
            ar.I32(ref State58); DrawingPreviewRecordModel.String(ar, ref LastString);
        }
    }
}
