using System;
using SldFile.Binary;
using SldFile.Drawing;

namespace SldFile.Mfc;

public sealed partial class MfcCompEdgeModel
{
    // Existing aggregate reads a bounded feature blob. Native graph loading
    // must stop at the derived object's boundary instead of ReadRemainingBytes.
    // descriptor 0x4DADD090 -> final vtable 0x4CEA5F58 -> 0x4BBF4F70
    // -> composite edge 0x4BBF4930 -> moCompBEntRef 0x4BBF3B10.
    internal sealed class NativeState : NativeDrawingComponentEntityReferenceModel
    {
        internal NativeDrawingObjectGraph.Node? Composite190, Composite198;
        internal uint Flags188, State18C;
        internal DrawingPreviewPoint Point158, Point170;
        internal NativeDrawingObjectGraph.Node?[] Entries = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal int[] DrawingStates1B4 = new int[4];

        public override void Serialize(MfcArchive ar)
        {
            base.Serialize(ar);
            var graph = NativeDrawingModelFactory.Graph(ar);
            ar.U32(ref Flags188);
            DrawingPreviewRecordModel.Point(ar, ref Point158); DrawingPreviewRecordModel.Point(ar, ref Point170);
            ar.U32(ref State18C);
            int count = Entries.Length; ar.I32(ref count);
            if (count < 0 || (ar.IsLoading && count > ar.Remaining / 2))
                throw new System.IO.InvalidDataException("Invalid native composite-edge entry count.");
            if (ar.IsLoading) Entries = new NativeDrawingObjectGraph.Node?[count];
            for (int i = 0; i < count; i++) graph.Object(ar, ref Entries[i]);
            if ((Flags188 & 6) != 6 && (Flags188 & 8) != 0)
            {
                graph.Object(ar, ref Composite190); graph.Object(ar, ref Composite198);
            }
            if (ar.DocumentVersion > 0x3351)
            {
                bool drawing = ar.Context.NativeDrawingOwnerIsDrawing
                    ?? throw new InvalidOperationException("Edge-reference drawing-owner context is required.");
                if (drawing)
                {
                    if (DrawingStates1B4.Length != 4) throw new System.IO.InvalidDataException("Four drawing edge states are required.");
                    for (int i = 0; i < 4; i++) ar.I32(ref DrawingStates1B4[i]);
                }
            }
        }
    }
}
