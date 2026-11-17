using System;
using System.IO;

namespace SldFile.Mfc;

// moReferenceInternal_c descriptor 0x4DAC8FD0, vtable 0x4CF92360.
// 0x4B6CC830 calls moReference 0x4B6CCC60 without another wire tail.
// This is a moFeature subclass, distinct from moCompRef selection wrappers.
internal sealed class NativeDrawingReferenceModel : IMfcSerializable
{
    internal NativeDrawingFeatureModel Feature = new NativeDrawingFeatureModel();
    internal byte HasTransform, HasBounds, HasLegacyTransform, HasTransforms;
    internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel(), LegacyTransform = new NativeDrawingTransformModel();
    internal double[] Bounds = new double[10];
    internal NativeDrawingObjectGraph.Node? ExternalObject, SmartPart, Smc, SmartInsert, Toolbox, XmlRules, ExternalFeature, Wrapper, StackPart;
    internal ushort State3F8, State400;
    internal uint State3D8, State3E8, Time3E0, State564, State3EC, State3F0;
    internal string? Configuration, State508, State510;
    internal int ConfigurationId, State448, State450, State4B8, State4BC, State44C, State4E0, State410, State414, State4C8, State4E4, State4F8, State558;
    internal int State52C, State538, State380, State4A0, State4A4, State4A8, State3D4;
    internal TransformEntry[] Transforms = Array.Empty<TransformEntry>();
    internal sealed class TransformEntry
    {
        internal byte Present;
        internal NativeDrawingTransformModel Value = new NativeDrawingTransformModel();
    }
    public void Serialize(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        if (version < 5000) throw new NotSupportedException("Unverified legacy drawing reference layout.");
        Feature.Serialize(ar);
        ar.Byte(ref HasTransform); if (HasTransform != 0) Transform.Serialize(ar);
        ar.Byte(ref HasBounds); if (HasBounds != 0) for (int i = 0; i < Bounds.Length; i++) ar.F64(ref Bounds[i]);
        var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref ExternalObject);
        ar.U16(ref State3F8); ar.U16(ref State400); ar.U32(ref State3D8);
        NativeDrawingReferencedConfiguration.Serialize(ar, ref Configuration, ref ConfigurationId);
        ar.Byte(ref HasLegacyTransform); if (HasLegacyTransform != 0) LegacyTransform.Serialize(ar);
        ar.Byte(ref HasTransforms);
        if (HasTransforms != 0)
        {
            int count = Transforms.Length; ar.I32(ref count);
            if (count < 0 || ar.IsLoading && count > ar.Remaining)
                throw new InvalidDataException("Invalid reference transform count.");
            if (ar.IsLoading) Transforms = new TransformEntry[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) Transforms[i] = new TransformEntry();
                ar.Byte(ref Transforms[i].Present); if (Transforms[i].Present != 0) Transforms[i].Value.Serialize(ar);
            }
        }
        ar.I32(ref State448); ar.U32(ref State3E8); graph.Object(ar, ref SmartPart); ar.I32(ref State450);
        graph.Object(ar, ref Smc); graph.Object(ar, ref SmartInsert); ar.I32(ref State4B8);
        graph.Object(ar, ref Toolbox); ar.I32(ref State4BC); ar.I32(ref State44C); graph.Object(ar, ref XmlRules);
        ar.I32(ref State4E0); ar.I32(ref State410); ar.I32(ref State414); ar.I32(ref State4C8); ar.I32(ref State4E4);
        ar.U32(ref Time3E0); ar.I32(ref State4F8); graph.Object(ar, ref ExternalFeature); ar.I32(ref State558);
        DrawingPreviewRecordModel.String(ar, ref State508); DrawingPreviewRecordModel.String(ar, ref State510);
        if (version > 0x1C50) ar.I32(ref State52C);
        if (version > 0x23D6) ar.I32(ref State538);
        if (version > 0x23F5) ar.I32(ref State380);
        if (version > 0x2410) ar.U32(ref State564);
        if (version > 0x2B6C) { ar.U32(ref State3EC); ar.U32(ref State3F0); }
        if (version > 0x2F53) graph.Object(ar, ref Wrapper);
        if (version > 0x2F7D)
        {
            graph.Object(ar, ref StackPart); ar.I32(ref State4A0); ar.I32(ref State4A4); ar.I32(ref State4A8);
        }
        if (version > 0x42D2) ar.I32(ref State3D4);
    }
}

// moExtFeature_c descriptor 0x4DAEEE20, vtable 0x4CF4B160 ->
// 0x4C1B8B00. GFR base 0x4C1B94E0 has an inline shared-file record,
// configuration and name handle, followed by a moReference back-reference.
// CmgrModel's aggregate component-name list is not this native payload.
internal sealed class NativeDrawingExternalFeatureModel : IMfcSerializable
{
    internal NativeDrawingSharedFileModel File = new NativeDrawingSharedFileModel();
    internal string? Configuration;
    internal int ConfigurationId, State44, State58;
    internal byte State40;
    internal NativeDrawingObjectGraph.Node? Name, Reference;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy external-feature layout.");
        File.Serialize(ar); NativeDrawingReferencedConfiguration.Serialize(ar, ref Configuration, ref ConfigurationId);
        ar.Byte(ref State40); ar.I32(ref State44);
        var graph = NativeDrawingModelFactory.Graph(ar);
        graph.Object(ar, ref Name); graph.Object(ar, ref Reference); ar.I32(ref State58);
    }
}

// sldarchiveu moReferencedConfiguration_c::Serialize 0x31D77DD0.
internal static class NativeDrawingReferencedConfiguration
{
    internal static void Serialize(MfcArchive ar, ref string? name, ref int id)
    {
        DrawingPreviewRecordModel.String(ar, ref name); ar.I32(ref id);
    }
}
