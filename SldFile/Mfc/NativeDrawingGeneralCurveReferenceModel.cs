using System;

namespace SldFile.Mfc;

// moGeneralCurveRef_w::Serialize 0x4C2AEA20 -> curve wrapper
// 0x4C2ACA10 -> moRefWrapper 0x4C2B46F0. This is also the verified
// base of moCircleRef_w; retain one implementation for both class tags.
internal class NativeDrawingGeneralCurveReferenceModel : NativeDrawingReferenceWrapperModel
{
    internal NativeDrawingObjectGraph.Node? ChooserD8, SketchHandleD0, ChooserE0;
    internal int StateCC;
    internal uint StateC8;
    internal double StateA8, StateB0;
    internal byte HasKernelBody;
    public override void Serialize(MfcArchive ar)
    {
        base.Serialize(ar);
        ar.F64(ref StateA8); ar.F64(ref StateB0);
        ar.Byte(ref HasKernelBody);
        if (HasKernelBody != 0) throw new NotSupportedException("Curve-reference inline kernel body is not decoded.");
        ar.U32(ref StateC8); ar.I32(ref StateCC);
        var graph = NativeDrawingModelFactory.Graph(ar);
        graph.Object(ar, ref ChooserD8); graph.Object(ar, ref SketchHandleD0); graph.Object(ar, ref ChooserE0);
    }
}
