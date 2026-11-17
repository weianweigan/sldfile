using System;
using SldFile.Binary;

namespace SldFile.Mfc;

internal sealed partial class DrNoteMfcModel
{
    internal NativeComponentState? NativeComponent;
    internal NativeDataState? NativeData;
    internal NativeDisplayState? NativeDisplay;

    // moDisplayNote_c descriptor 0x4DB06340, vtable 0x4D23EEE0 ->
    // 0x4C972F00. The graph owns the tag; all fields follow moDisplayItem.
    internal sealed class NativeDisplayState
    {
        internal NativeDrawingAnnotationItemModel Item = new NativeDrawingAnnotationItemModel();
        internal ushort State5E4, State5E8, State5EC, State5E0;
        internal SldFile.Drawing.DrawingPreviewPoint Vector5C8;
        internal double State5D0, State638, State6B0;
        internal int State630, State634, LegacyFlags708, State5B8, State5BC, State5C0, State724, State728, State72C;
        internal uint State710;
        internal uint[] FlagWords = Array.Empty<uint>();
        internal string? State5F8, State600, State608, State678;
        internal NativeDrawingObjectGraph.Node? Hyperlink, StringMap, Stacked, Object5A0, Object680, Bom, CosmeticThread, IntegerMap;
        internal void Serialize(MfcArchive ar)
        {
            int version = ar.DocumentVersion;
            if (version < 5000) throw new NotSupportedException("Unverified legacy display-note layout.");
            // These versions contain discarded migration records and move
            // watermark data between owners. Keep that protocol explicit.
            if (version > 0x1BFA && version < 0x277D)
                throw new NotSupportedException("Obsolete display-note migration layout is not decoded.");
            Item.Serialize(ar);
            ar.U16(ref State5E4); ar.U16(ref State5E8); ar.U16(ref State5EC);
            DrawingPreviewRecordModel.Point(ar, ref Vector5C8); ar.F64(ref State5D0);
            ar.I32(ref State630); ar.I32(ref State634);
            var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref Hyperlink);
            DrawingPreviewRecordModel.String(ar, ref State5F8); DrawingPreviewRecordModel.String(ar, ref State600);
            DrawingPreviewRecordModel.String(ar, ref State608); graph.Object(ar, ref StringMap); graph.Object(ar, ref Stacked);
            if (version < 0x1C0B) ar.I32(ref LegacyFlags708);
            else
            {
                int count = FlagWords.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                if (ar.IsLoading)
                {
                    if (count > ar.Remaining / 4) throw new System.IO.InvalidDataException("Invalid native note flag-word count.");
                    FlagWords = new uint[count];
                }
                for (int i = 0; i < count; i++) ar.U32(ref FlagWords[i]);
            }
            graph.Object(ar, ref Object5A0); ar.F64(ref State638);
            DrawingPreviewRecordModel.String(ar, ref State678); graph.Object(ar, ref Object680); ar.U16(ref State5E0);
            graph.Object(ar, ref Bom); ar.I32(ref State5B8); ar.I32(ref State5BC);
            if (version >= 0x1429) ar.F64(ref State6B0);
            if (version >= 0x17E1) ar.I32(ref State5C0);
            if (version >= 0x1C00) ar.U32(ref State710);
            if (version > 0x200C) ar.I32(ref State724);
            if (version > 0x2BBB) ar.I32(ref State728);
            if (version > 0x2F77) ar.I32(ref State72C);
            if (version > 0x2F9A) graph.Object(ar, ref CosmeticThread);
            if (version > 0x2FB7) graph.Object(ar, ref IntegerMap);
        }
    }

    // moNoteData_c descriptor 0x4DB061C0, vtable 0x4D23E200 ->
    // 0x4C9807D0. moDetailItem 0x4BA9B6C0 uses moNode's LW serializer
    // (0x4C1BAEE0) and an empty resolved-data virtual (0x4B2DD7C0).
    internal sealed class NativeDataState
    {
        internal NativeDrawingNodeLightweightModel Node = new NativeDrawingNodeLightweightModel();
        internal ushort State48;
        internal NativeDrawingObjectGraph.Node? Owner, Object80, Text;
        internal NativeDrawingObjectGraph.Node?[] Attachments = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal int[] Codes = Array.Empty<int>();
        internal int State78;
        internal void Serialize(MfcArchive ar)
        {
            SerializeDetailItem(ar);
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref Text);
        }

        // moDetailItem is also the base of center-mark data. Text belongs
        // to moNoteData, so keep it outside this reusable native base.
        internal void SerializeDetailItem(MfcArchive ar)
        {
            if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native note data.");
            Node.SerializeNode(ar); ar.U16(ref State48);
            var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref Owner);
            NativeDrawingObjectArray.Serialize(ar, ref Attachments);
            int count = Codes.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) Codes = new int[count];
            for (int i = 0; i < count; i++) ar.I32(ref Codes[i]);
            ar.I32(ref State78); graph.Object(ar, ref Object80);
        }
    }

    // moCompNote_c descriptor 0x4DB06A60, vtable 0x4CF0A740,
    // Serialize 0x4C96D2E0 delegates to moCompDetailItem 0x4C96CD50.
    // Unlike moDisplayNote_c, it has no display-item or text-scanning body.
    internal sealed class NativeComponentState : NativeComponentReferenceState, IMfcSerializable
    {
        internal NativeDrawingObjectGraph.Node? Item, Backup;
        internal string? ExternalItem, ExternalDocument;
        internal int State100;
        public void Serialize(MfcArchive ar)
        {
            bool external = ar.Context.NativeAnnotationReferenceIsExternal
                ?? throw new InvalidOperationException("Native annotation component requires explicit filed-owner reference context.");
            SerializeReferenceBase(ar);
            var graph = NativeDrawingModelFactory.Graph(ar);
            if (external)
            {
                DrawingPreviewRecordModel.String(ar, ref ExternalItem); DrawingPreviewRecordModel.String(ar, ref ExternalDocument);
            }
            else graph.Object(ar, ref Item);
            graph.Object(ar, ref Backup); ar.I32(ref State100);
        }
    }
}
