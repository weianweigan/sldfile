using System;
using SldFile.Binary;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moRefDim2E_c descriptor 0x4DB02DC8 -> constructor 0x4C295210,
// vtable 0x4D148B08 -> Serialize 0x4C856CD0 -> moRefDim 0x4C858350.
// Dimension_c is already decoded for sketch dimensions. Reuse that base;
// moRefDim's references and transform are a distinct native derived payload.
internal sealed class NativeDrawingReferenceDimensionModel : IMfcSerializable
{
    internal bool SingleEntity;
    internal ushort SingleStateF0, SingleStateF4;
    internal SketchData.NativeDimension Dimension = new SketchData.NativeDimension();
    internal NativeDrawingTransformModel Transform80 = new NativeDrawingTransformModel();
    internal byte HasVector88, HasVector90;
    internal DrawingPreviewPoint Vector88, Vector90;
    internal int StateA4, StateB0, StateA0, StateC4, StateC8, StateD0;
    internal NativeDrawingObjectGraph.Node? PlaneTagA8, SketchHandleB8, FirstE0, SecondF8;
    internal ushort FirstTypeD8 = 8, SecondTypeF0 = 8, State108, State110, State114, State118, State11C, State10C;

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy reference-dimension layout.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Reference-dimension export layout is not decoded.");
        Dimension.Serialize(ar); Transform80.Serialize(ar);
        ar.Byte(ref HasVector88); if (HasVector88 != 0) DrawingPreviewRecordModel.Point(ar, ref Vector88);
        ar.Byte(ref HasVector90); if (HasVector90 != 0) DrawingPreviewRecordModel.Point(ar, ref Vector90);
        var graph = NativeDrawingModelFactory.Graph(ar);
        ar.I32(ref StateA4); graph.Object(ar, ref PlaneTagA8); ar.I32(ref StateB0);
        graph.Object(ar, ref SketchHandleB8); ar.I32(ref StateA0); ar.I32(ref StateC4); ar.I32(ref StateC8);
        if (ar.DocumentVersion >= 0x46C7) ar.I32(ref StateD0);
        ar.U16(ref FirstTypeD8); graph.Object(ar, ref FirstE0);
        // moRefDim1E 0x4C856740 shares the exact same moRefDim base
        // and first entity record. It appends only these two state words,
        // with the second introduced at 0x2F6F.
        if (SingleEntity)
        {
            ar.U16(ref SingleStateF0);
            if (ar.DocumentVersion >= 0x2F6F) ar.U16(ref SingleStateF4);
            return;
        }
        ar.U16(ref SecondTypeF0); graph.Object(ar, ref SecondF8);
        ar.U16(ref State108); ar.U16(ref State110); ar.U16(ref State114);
        ar.U16(ref State118); ar.U16(ref State11C);
        if (ar.DocumentVersion > 0x3747) ar.U16(ref State10C);
    }
}
