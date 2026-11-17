using System;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moRefDimHandle_c descriptor 0x4DB03248, final vtable 0x4CDB5348
// -> moModDimHandle::Serialize 0x4C8563B0 -> shared 0x4C84C520.
// No matching native moRefDimHandle model exists in the aggregate dimension
// types. Reuse the actual moCompRef base, not a sketch dimension aggregate.
internal sealed class NativeDrawingDimensionHandleModel : SldFile.Binary.NativeComponentReferenceState, IMfcSerializable
{
    internal ushort HasTransform120, HasTransform128;
    internal NativeDrawingTransformModel Transform120 = new NativeDrawingTransformModel(), Transform128 = new NativeDrawingTransformModel(), CachedTransform108 = new NativeDrawingTransformModel();
    internal NativeDrawingObjectGraph.Node? ViewTag130, ViewTag138, ViewTag140, ViewTag148, GlobalReference, CachedDimension, Dimension;
    internal byte IsExternal;
    internal int HasCachedName, State154, State118;
    internal string? CachedName;
    internal DrawingPreviewPoint Point180, Point198, Point1B0, Point1C8;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy dimension-handle layout.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Dimension-handle export layout is not decoded.");
        SerializeReferenceBase(ar);
        var graph = NativeDrawingModelFactory.Graph(ar);
        ar.U16(ref HasTransform120); if (HasTransform120 != 0) Transform120.Serialize(ar);
        graph.Object(ar, ref ViewTag130); graph.Object(ar, ref ViewTag138);
        graph.Object(ar, ref ViewTag140); graph.Object(ar, ref ViewTag148);
        ar.Byte(ref IsExternal); graph.Object(ar, ref GlobalReference); graph.Object(ar, ref CachedDimension);
        if (CachedDimension != null)
        {
            CachedTransform108.Serialize(ar); ar.I32(ref HasCachedName);
            if (HasCachedName != 0) DrawingPreviewRecordModel.String(ar, ref CachedName);
        }
        ar.I32(ref State154); ar.U16(ref HasTransform128); if (HasTransform128 != 0) Transform128.Serialize(ar);
        if (ar.DocumentVersion >= 0x335D)
        {
            bool ownerIsDrawing = ar.Context.NativeDrawingOwnerIsDrawing
                ?? throw new InvalidOperationException("Dimension-handle drawing-owner context is required.");
            if (ownerIsDrawing)
            {
                DrawingPreviewRecordModel.Point(ar, ref Point180); DrawingPreviewRecordModel.Point(ar, ref Point198);
                DrawingPreviewRecordModel.Point(ar, ref Point1B0); DrawingPreviewRecordModel.Point(ar, ref Point1C8);
            }
        }
        ar.I32(ref State118);
        // The ordinary moModDimHandle tail stores its dimension only when
        // the base has no GFR. condition(1) uses iSerializeInternal instead.
        if (GlobalReference == null) graph.Object(ar, ref Dimension);
    }
}
