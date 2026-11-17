using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

public abstract partial class MoDisplayDimModel
{
    internal NativeState? NativeDimension;

    // moDisplayDistanceDim_c descriptor 0x4DB02A98, final vtable
    // 0x4D262670 -> 0x4C852DF0 -> moDisplayDim 0x4C84E0E0.
    // This explicit native entry leaves the existing aggregate API intact.
    internal sealed class NativeState
    {
        internal bool Angular;
        internal NativeRadialState? RadialDimension;
        internal ushort AngularState100C, AngularState1010;
        internal byte AngularState1008;
        internal DrawingPreviewPoint AngularVectorFB8, AngularVectorFD0;
        internal double[] AngularValuesFE8 = new double[4];
        internal int AngularState1018;
        internal NativeDrawingAnnotationItemModel Item = new NativeDrawingAnnotationItemModel();
        internal NativeDrawingDimTextModel Text = new NativeDrawingDimTextModel();
        internal NativeDrawingObjectGraph.Node?[] Handles = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal ushort State870, State874, State878, State898, State89C, State8A0, State8C0, State8E4, State8EC, State8F4, State8E8, State8F0, StateB68, StateB6C, State8FC;
        internal string? State7F8, State800, State818, State820, StateA70, State680, StateB70, StateB78, StateB80, StateB88, State808, State810, StateF60, StateF88;
        internal DrawingPreviewPoint Vector880, VectorA38, Point958, Point970, Point988, Point9A0, Point9B8;
        internal double State8A8, State8B0, State950, StateA78, StateAD0, StateB40, State830, State838, State840, State848, State8B8, State648, State8C8;
        internal int HasA50, HasPoint590, HasPoint598, State8F8, StateAF4, State900, StateB9C, StateA90, HasPointAC0, HasPointAC8;
        internal int StateB4C, StateB50, StateD78, StateD7C, StateF00, State8D0, StateF08, StateF0C, StateF10, State8D8, State8DC, StateD80, StateF84, StateF54, State650, StateF80, StateF58, State8E0;
        internal uint Flags8A4, FlagsB60, StateE40, FlagsB64;
        internal NativeDrawingObjectGraph.Node? PointRef580, PointRef588, LengthUnits, AngleUnits, Favorite, JogA80, JogA88, LinkedDimension, ViewTagAB0, ViewTagAB8, Backup, Radial;
        internal double State5A0, State5A8;
        internal DrawingPreviewPoint Point590, Point598, PointA50, VectorA68, PointAC0, PointAC8, PointF68, PointFE8;
        internal double StateA80;
        internal NativeDrawingGeometryFeatureModel.OptionalArray ObjectsA58 = new NativeDrawingGeometryFeatureModel.OptionalArray(), ObjectsA60 = new NativeDrawingGeometryFeatureModel.OptionalArray();
        internal NativeDrawingArrayModel Steps = new NativeDrawingArrayModel();
        internal NativeDrawingTransformModel Transform5C0 = new NativeDrawingTransformModel();
        internal NativeDrawingObjectListModel ObjectsE48 = new NativeDrawingObjectListModel(), Objects908 = new NativeDrawingObjectListModel();
        internal int State1014, State1000, State1010;
        internal double StateFB8, StateFC0;

        internal void Serialize(MfcArchive ar)
        {
            int version = ar.DocumentVersion;
            if (version < 5000) throw new NotSupportedException("Unverified legacy native display-dimension layout.");
            if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
                throw new NotSupportedException("Display-dimension export archive conditions are not decoded.");
            Item.Serialize(ar); Text.Serialize(ar);
            var graph = NativeDrawingModelFactory.Graph(ar);
            int handles = Handles.Length; NativeDrawingDisplayArchive.Count(ar, ref handles);
            if (ar.IsLoading) Handles = new NativeDrawingObjectGraph.Node?[handles];
            for (int i = 0; i < handles; i++) graph.Object(ar, ref Handles[i]);
            ar.U16(ref State870); ar.U16(ref State874); ar.U16(ref State878);
            DrawingPreviewRecordModel.String(ar, ref State7F8); DrawingPreviewRecordModel.String(ar, ref State800);
            DrawingPreviewRecordModel.String(ar, ref State818); DrawingPreviewRecordModel.String(ar, ref State820);
            DrawingPreviewRecordModel.Point(ar, ref Vector880); ar.F64(ref State8A8); ar.F64(ref State8B0);
            ar.F64(ref State950); ar.F64(ref StateA78);
            DrawingPreviewRecordModel.Point(ar, ref Point958); DrawingPreviewRecordModel.Point(ar, ref Point970);
            DrawingPreviewRecordModel.Point(ar, ref Point988); DrawingPreviewRecordModel.Point(ar, ref Point9A0); DrawingPreviewRecordModel.Point(ar, ref Point9B8);
            ar.I32(ref HasA50);
            if (HasA50 != 0)
            {
                DrawingPreviewRecordModel.Point(ar, ref PointA50); DrawingPreviewRecordModel.Point(ar, ref VectorA68); ar.F64(ref StateA80);
            }
            ar.U16(ref State898); ar.U16(ref State89C); ar.U16(ref State8A0); ar.U16(ref State8C0);
            ar.U16(ref State8E4); ar.U16(ref State8EC); ar.U16(ref State8F4);
            DrawingPreviewRecordModel.Point(ar, ref VectorA38);
            // Distance/angular/radial return 1 at +A78. Radial's +8D8
            // returns 1, but its load gate is >0x233, already enabled by
            // the minimum 5000. All three use this same ordinary record.
            graph.Object(ar, ref PointRef580); graph.Object(ar, ref PointRef588);
            ar.F64(ref State5A0); ar.F64(ref State5A8);
            OptionalPoint(ar, ref HasPoint590, ref Point590); OptionalPoint(ar, ref HasPoint598, ref Point598);
            ar.U16(ref State8E8); ar.U16(ref State8F0); ar.U32(ref Flags8A4);
            ObjectsA58.Serialize(ar); ObjectsA60.Serialize(ar); DrawingPreviewRecordModel.String(ar, ref StateA70);
            graph.Object(ar, ref LengthUnits); graph.Object(ar, ref AngleUnits); ar.I32(ref State8F8); ar.F64(ref StateAD0);
            Steps.Serialize(ar);
            if (Steps.ElementSize != 0 && Steps.ElementSize != 8)
                throw new InvalidDataException("Native dimension step array requires eight-byte elements.");
            DrawingPreviewRecordModel.String(ar, ref State680); graph.Object(ar, ref Favorite); ar.F64(ref StateB40);
            ar.U16(ref StateB68); ar.U16(ref StateB6C); ar.U32(ref FlagsB60);
            ar.F64(ref State830); ar.F64(ref State838); ar.F64(ref State840); ar.F64(ref State848); ar.I32(ref StateAF4);
            ar.F64(ref State8B8); ar.U16(ref State8FC); ar.I32(ref State900);
            Transform5C0.SerializeOperator(ar); ar.F64(ref State648);
            DrawingPreviewRecordModel.String(ar, ref StateB70); DrawingPreviewRecordModel.String(ar, ref StateB78);
            DrawingPreviewRecordModel.String(ar, ref StateB80); DrawingPreviewRecordModel.String(ar, ref StateB88);
            ObjectsE48.Serialize(ar); Objects908.Serialize(ar); ar.I32(ref StateB9C); ar.U32(ref StateE40);
            graph.Object(ar, ref JogA80); graph.Object(ar, ref JogA88); graph.Object(ar, ref LinkedDimension); ar.U32(ref FlagsB64);
            graph.Object(ar, ref ViewTagAB0); graph.Object(ar, ref ViewTagAB8);
            OptionalPoint(ar, ref HasPointAC0, ref PointAC0); OptionalPoint(ar, ref HasPointAC8, ref PointAC8);
            ar.I32(ref StateA90); graph.Object(ar, ref Backup);
            if (version >= 0x13F3) { ar.I32(ref StateB4C); ar.I32(ref StateB50); }
            if (version >= 0x1405) { ar.I32(ref StateD78); ar.I32(ref StateD7C); }
            if (version > 0x140E) ar.I32(ref StateF00);
            if (version > 0x1464) ar.F64(ref State8C8);
            if (version > 0x17D6) ar.I32(ref State8D0);
            if (version > 0x1815) { ar.I32(ref StateF08); ar.I32(ref StateF0C); ar.I32(ref StateF10); }
            if (version > 0x1828) { ar.I32(ref State8D8); ar.I32(ref State8DC); }
            if (version > 0x182C) DrawingPreviewRecordModel.String(ar, ref State808);
            if (version > 0x182F) DrawingPreviewRecordModel.String(ar, ref State810);
            if (version > 0x1843) ar.I32(ref StateD80);
            if (version > 0x1FA6) ar.I32(ref StateF84);
            if (version > 0x1FBB)
            {
                ar.I32(ref StateF54); DrawingPreviewRecordModel.String(ar, ref StateF60);
                ar.I32(ref State650); ar.I32(ref StateF80); DrawingPreviewRecordModel.Point(ar, ref PointF68);
            }
            if (version > 0x1FC2) ar.I32(ref StateF58);
            if (version > 0x23AE) ar.I32(ref State8E0);
            if (version >= 0x27E0) DrawingPreviewRecordModel.String(ar, ref StateF88);
            // Native base's remaining ordinary-version work repairs runtime
            // geometry and links; it does not append archive fields.
            if (RadialDimension != null) { RadialDimension.Serialize(ar); return; }
            if (Angular)
            {
                // moDisplayAngularDim 0x4C84D5C0 -> 0x4C84D600.
                // All derived load gates precede the supported version 5000.
                ar.U16(ref AngularState100C); ar.U16(ref AngularState1010); ar.Byte(ref AngularState1008);
                DrawingPreviewRecordModel.Point(ar, ref AngularVectorFB8); DrawingPreviewRecordModel.Point(ar, ref AngularVectorFD0);
                if (AngularValuesFE8.Length != 4) throw new InvalidDataException("Four angular display values are required.");
                for (int i = 0; i < 4; i++) ar.F64(ref AngularValuesFE8[i]);
                ar.I32(ref AngularState1018);
                return;
            }
            ar.I32(ref State1014); ar.F64(ref StateFB8); ar.F64(ref StateFC0);
            DrawingPreviewRecordModel.Point(ar, ref PointFE8); ar.I32(ref State1000); graph.Object(ar, ref Radial);
            if (version > 0x3747) ar.I32(ref State1010);
        }

        private static void OptionalPoint(MfcArchive ar, ref int present, ref DrawingPreviewPoint point)
        {
            ar.I32(ref present); if (present != 0) DrawingPreviewRecordModel.Point(ar, ref point);
        }
    }
}
