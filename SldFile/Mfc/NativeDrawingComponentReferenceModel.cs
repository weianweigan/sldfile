using System;

namespace SldFile.Mfc;

// moComponent::Serialize 0x4C25A6A0, ordinary >=5000 archive branch.
// The referenced object is the filed model or an external-object record.
internal sealed class NativeDrawingComponentReferenceModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? Owner;
    internal byte HasExternalObject;
    internal NativeDrawingObjectGraph.Node? ExternalObject;
    internal bool Chain;
    internal NativeDrawingObjectGraph.Node? ReferenceChain;
    internal uint ChainState30;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy component reference.");
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref Owner);
        if (Chain)
        {
            // moChainComponent 0x4C2594A0 shares moComponent. Its +8
            // legacy name cache remains null in the ordinary >=5000 load
            // path; it is not the serialized Owner pointer. 0x4B88AC20
            // only updates the runtime model ID at +10.
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref ReferenceChain); ar.U32(ref ChainState30);
            return;
        }
        // moUnitComponent::Serialize 0x4C2698F0 appends the optional
        // external filed-model reference after the moComponent base.
        ar.Byte(ref HasExternalObject);
        if (HasExternalObject != 0) NativeDrawingModelFactory.Graph(ar).Object(ar, ref ExternalObject);
    }
}

// moTransRefPlaneData::Serialize 0x4C717700.
internal sealed class NativeDrawingReferencePlaneStyleModel : IMfcSerializable
{
    internal int ShowEdges, ShowIntersections, Transparency;
    internal uint FrontColor, BackColor, IntersectionColor;
    public void Serialize(MfcArchive ar)
    {
        ar.I32(ref ShowEdges); ar.I32(ref ShowIntersections);
        ar.U32(ref FrontColor); ar.U32(ref BackColor);
        ar.I32(ref Transparency); ar.U32(ref IntersectionColor);
    }
}
