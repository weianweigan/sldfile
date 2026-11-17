using System;
using System.IO;

namespace SldFile.Mfc;

internal sealed partial class DrSheetMfcModel
{
    internal NativeState? NativeSheet;

    // moDrSheet descriptor 0x4DB06FD0, vtable 0x4D286048,
    // Serialize 0x4C975F40. The template uses this same base payload.
    // Both vtables inherit moConcept's LW/resolved serializers.
    internal sealed class NativeState
    {
        internal NativeDrawingModelFeatureModel Feature = new NativeDrawingModelFeatureModel();
        internal double State3F0, State3F8, State3E0, State3E8;
        internal byte State394, State3B8, HasProfile400, HasProfile460, HasProfile410;
        internal byte State3A8, State418, State420, HasTransform428, HasOverrides448;
        internal NativeDrawingObjectGraph.Node? Profile400, Profile460, Profile410, Template, Views, DetailFolder;
        internal string? State3B0, State3C8;
        internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
        internal int State440, State3D0, State46C, State4C0, State4C4, State4C8, State4CC, State4D0 = 1, State4D8 = 1;
        internal double State4A0, State4A8, State4B0, State4B8;
        internal ushort ActiveXMarker = ushort.MaxValue;
        internal uint Time4E8;
        internal OverrideEntry[] Overrides = Array.Empty<OverrideEntry>();
        internal NativeDrawingObjectGraph.Node?[] InkViews = Array.Empty<NativeDrawingObjectGraph.Node?>();

        internal sealed class OverrideEntry
        {
            internal NativeDrawingObjectGraph.Node? Handle;
            // Same moLineViz_c::Serialize(param3=0) as native sgSketch.
            internal SldFile.Binary.SketchData.NativeLineVisual Visual = new SldFile.Binary.SketchData.NativeLineVisual();
            internal int State1C, State20;
            internal void Serialize(MfcArchive ar)
            {
                NativeDrawingModelFactory.Graph(ar).Object(ar, ref Handle);
                Visual.Serialize(ar); ar.I32(ref State1C); ar.I32(ref State20);
            }
        }

        internal void Serialize(MfcArchive ar)
        {
            int version = ar.DocumentVersion;
            if (version < 5000) throw new NotSupportedException("Unverified legacy drawing-sheet layout.");
            // Native loading discards obsolete border migration records in
            // this interval. Do not silently omit them when writing back.
            if (version > 0x1FD0 && version < 0x200D)
                throw new NotSupportedException("Obsolete drawing-sheet border migration layout is not decoded.");
            Feature.Serialize(ar);
            ar.F64(ref State3F0); ar.F64(ref State3F8); ar.Byte(ref State394); ar.Byte(ref State3B8);
            ar.F64(ref State3E0); ar.F64(ref State3E8);
            var graph = NativeDrawingModelFactory.Graph(ar);
            ar.Byte(ref HasProfile400); if (HasProfile400 != 0) graph.Object(ar, ref Profile400);
            ar.Byte(ref HasProfile460); if (HasProfile460 != 0) graph.Object(ar, ref Profile460);
            ar.Byte(ref HasProfile410); if (HasProfile410 != 0) graph.Object(ar, ref Profile410);
            ar.Byte(ref State3A8); ar.Byte(ref State418); ar.Byte(ref State420);
            DrawingPreviewRecordModel.String(ar, ref State3B0); graph.Object(ar, ref Template);
            graph.Object(ar, ref Views);
            ar.Byte(ref HasTransform428); if (HasTransform428 != 0) Transform.Serialize(ar);
            graph.Object(ar, ref DetailFolder); ar.I32(ref State440);
            ar.Byte(ref HasOverrides448);
            if (HasOverrides448 != 0)
            {
                // 0x4C579350 -> inline entries 0x4C975DB0 (no MFC tag).
                int count = Overrides.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                if (ar.IsLoading)
                {
                    if (count > ar.Remaining / 25) throw new InvalidDataException("Invalid sheet override count.");
                    Overrides = new OverrideEntry[count];
                }
                for (int i = 0; i < count; i++)
                {
                    if (ar.IsLoading) Overrides[i] = new OverrideEntry();
                    Overrides[i].Serialize(ar);
                }
            }
            DrawingPreviewRecordModel.String(ar, ref State3C8);
            ar.I32(ref State3D0); ar.U16(ref ActiveXMarker);
            if (ActiveXMarker != ushort.MaxValue)
                throw new NotSupportedException("Drawing-sheet ActiveX control payload is not decoded.");
            ar.I32(ref State46C);
            if (version >= 0x1BC1)
            {
                ar.F64(ref State4A0); ar.F64(ref State4A8); ar.F64(ref State4B0); ar.F64(ref State4B8);
                ar.I32(ref State4C0); ar.I32(ref State4C4);
                if (version >= 0x1BF9) ar.I32(ref State4C8);
                if (version >= 0x1C1B) ar.I32(ref State4CC);
                if (version > 0x1C52) ar.I32(ref State4D0);
            }
            if (version >= 0x1FD0) ar.I32(ref State4D8);
            if (version > 0x23AB) ar.U32(ref Time4E8);
            if (version > 0x2F48)
            {
                int count = InkViews.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                if (ar.IsLoading)
                {
                    if (count > ar.Remaining / 2) throw new InvalidDataException("Invalid sheet ink-view count.");
                    InkViews = new NativeDrawingObjectGraph.Node?[count];
                }
                for (int i = 0; i < count; i++) graph.Object(ar, ref InkViews[i]);
            }
        }
    }
}

internal sealed partial class DrTemplateMfcModel
{
    // This is the native descriptor name, without the aggregate model's _c.
    internal const string NativeClassName = "moDrTemplate";
    internal NativeState? NativeTemplate;
    // Descriptor 0x4DB07060, vtable 0x4D287FF8 -> 0x4C977A90.
    internal sealed class NativeState
    {
        internal DrSheetMfcModel.NativeState Sheet = new DrSheetMfcModel.NativeState();
        internal NativeDrawingObjectGraph.Node? Bom, TitleBlock, EditBorder;
        internal void Serialize(MfcArchive ar)
        {
            Sheet.Serialize(ar);
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref Bom); graph.Object(ar, ref TitleBlock);
            if (ar.DocumentVersion > 0x1FF6) graph.Object(ar, ref EditBorder);
            // 0x4C9E2980 only repairs in-memory sketch/display ownership.
        }
    }
}
