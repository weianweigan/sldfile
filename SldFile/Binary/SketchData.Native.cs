using System;
using System.IO;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    // Native wire state belongs to the existing sgSketch type. Geometry is
    // projected through the existing SketchPointModel, not a duplicate point API.
    internal NativePoint[] NativePoints = Array.Empty<NativePoint>();
    internal NativeLine[] NativeLines = Array.Empty<NativeLine>();
    internal NativeArc[] NativeArcs = Array.Empty<NativeArc>();
    internal NativeText[] NativeTexts = Array.Empty<NativeText>();
    internal NativeEllipse[] NativeEllipses = Array.Empty<NativeEllipse>();

    internal void SerializeNativeEllipseTable(MfcArchive ar, bool ownerIs3D)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch ellipse table.");
        int count = NativeEllipses.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) NativeEllipses = new NativeEllipse[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) NativeEllipses[i] = new NativeEllipse();
            var row = NativeEllipses[i]; row.Serialize(ar, ownerIs3D);
            if (ar.IsLoading)
            {
                row.Geometry.StartPoint = PointAt(row.Geometry.StartPointIndex);
                row.Geometry.EndPoint = PointAt(row.Geometry.EndPointIndex);
                row.Geometry.CenterPoint = PointAt(row.Geometry.CenterPointIndex);
                row.Geometry.MajorPoint = PointAt(row.Geometry.MajorPointIndex);
                row.Geometry.MinorPoint = PointAt(row.Geometry.MinorPointIndex);
            }
        }
    }

    internal sealed class NativeEllipse
    {
        internal ushort KindFlags;
        internal NativeLine Base = new NativeLine();
        internal SketchSegmentModel Geometry => Base.Geometry;
        internal int StateD8, MajorAntiPointIndex, MinorAntiPointIndex;
        internal bool CenterExtended, MajorExtended, MinorExtended, MajorAntiExtended, MinorAntiExtended;
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            // Sixth table: 0x4C5B0050 -> 0x4C5B99C0. Native getters
            // 0x4C59D2D0/0x4C59D4D0 confirm +E0/+E4 major/minor points.
            ar.U16(ref KindFlags); Base.SerializeBase(ar, ownerIs3D); ar.I32(ref StateD8);
            int center = Geometry.CenterPointIndex, major = Geometry.MajorPointIndex, minor = Geometry.MinorPointIndex;
            NativeLine.Endpoint(ar, ref center, ref CenterExtended); NativeLine.Endpoint(ar, ref major, ref MajorExtended);
            NativeLine.Endpoint(ar, ref minor, ref MinorExtended);
            NativeLine.Endpoint(ar, ref MajorAntiPointIndex, ref MajorAntiExtended);
            NativeLine.Endpoint(ar, ref MinorAntiPointIndex, ref MinorAntiExtended);
            Geometry.CenterPointIndex = center; Geometry.MajorPointIndex = major; Geometry.MinorPointIndex = minor;
            Geometry.Kind = SketchSegmentKind.Ellipse;
        }
    }

    internal void SerializeNativeTextTable(MfcArchive ar, bool ownerIs3D)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch text table.");
        int count = NativeTexts.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) NativeTexts = new NativeText[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) NativeTexts[i] = new NativeText();
            var row = NativeTexts[i]; row.Serialize(ar, ownerIs3D);
            if (ar.IsLoading)
            {
                row.Geometry.StartPoint = PointAt(row.Geometry.StartPointIndex);
                row.Geometry.EndPoint = PointAt(row.Geometry.EndPointIndex);
            }
        }
    }

    internal sealed class NativeText
    {
        internal NativeLine Base = new NativeLine();
        internal SketchSegmentModel Geometry => Base.Geometry;
        internal NativeDrawingObjectGraph.Node? DimensionText;
        internal string? SourceText;
        internal int StateF0, StateF4;
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            // 0x4C5C6EF0: text entities have no line-table discriminator.
            Base.SerializeBase(ar, ownerIs3D);
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref DimensionText);
            if (DimensionText?.Payload is not NativeDrawingDimTextModel text)
                throw new InvalidDataException("Native sketch text requires moDimText_c.");
            if (text.StateA0 != 0)
                throw new NotSupportedException("Native sketch text with associated curve/kernel payload is not decoded yet.");
            if (ar.IsStoring) SourceText = Geometry.Text;
            DrawingPreviewRecordModel.String(ar, ref SourceText); ar.I32(ref StateF0); ar.I32(ref StateF4);
            Geometry.Kind = SketchSegmentKind.Text; Geometry.Text = SourceText;
        }
    }

    // Fifth entity table in sgSketch. Text/hatch tables precede this table;
    // callers must decode those tables rather than search for this payload.
    internal void SerializeNativeArcTable(MfcArchive ar, bool ownerIs3D)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch arc table.");
        int count = NativeArcs.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) NativeArcs = new NativeArc[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) NativeArcs[i] = new NativeArc();
            var row = NativeArcs[i]; row.Serialize(ar, ownerIs3D);
            if (ar.IsLoading)
            {
                int start = row.Geometry.StartPointIndex, end = row.Geometry.EndPointIndex, center = row.Geometry.CenterPointIndex;
                row.Geometry.StartPoint = PointAt(start); row.Geometry.EndPoint = PointAt(end);
                row.Geometry.CenterPoint = PointAt(center);
            }
        }
    }

    private SketchPointModel? PointAt(int index) => index >= 0 && index < NativePoints.Length ? NativePoints[index].Geometry : null;

    internal sealed class NativeArc
    {
        internal ushort KindFlags;
        internal NativeLine Base = new NativeLine();
        internal SketchSegmentModel Geometry => Base.Geometry;
        internal int StateD8, StateE0, StateE4, StateE8, StateEC;
        internal bool CenterExtended;

        // 0x4C5AE400 -> 0x4C5B99C0. This is an inline arc entity,
        // not the sgArcHandle MFC reference object.
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            ar.U16(ref KindFlags); Base.SerializeBase(ar, ownerIs3D);
            ar.I32(ref StateD8);
            int center = Geometry.CenterPointIndex;
            NativeLine.Endpoint(ar, ref center, ref CenterExtended); Geometry.CenterPointIndex = center;
            ar.I32(ref StateE0); ar.I32(ref StateE4); ar.I32(ref StateE8); ar.I32(ref StateEC);
            Geometry.Kind = SketchSegmentKind.Arc;
            // sgArcHandle::isCircle (0x4C58C250) compares the +C8/+CC
            // indices through 0x4C4BFAD0/0x4C4BFA60, not point coordinates.
            Geometry.IsCircle = Geometry.StartPointIndex == Geometry.EndPointIndex;
        }
    }

    // Second native entity table: i32 count, then a u16 discriminator and
    // the shared line payload for each entry (0x4C5B3F40 -> 0x4C5B99C0).
    internal void SerializeNativeLineTable(MfcArchive ar, bool ownerIs3D)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch line table.");
        int count = NativeLines.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) NativeLines = new NativeLine[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) NativeLines[i] = new NativeLine();
            var row = NativeLines[i]; row.Serialize(ar, ownerIs3D);
            if (ar.IsLoading)
            {
                int start = row.Geometry.StartPointIndex, end = row.Geometry.EndPointIndex;
                row.Geometry.StartPoint = start >= 0 && start < NativePoints.Length ? NativePoints[start].Geometry : null;
                row.Geometry.EndPoint = end >= 0 && end < NativePoints.Length ? NativePoints[end].Geometry : null;
            }
        }
    }

    internal sealed class NativeLine
    {
        internal ushort KindFlags, StateD0, StateD4, StateE8;
        internal bool StartExtended, EndExtended;
        internal NativeEntityFields Entity = new NativeEntityFields();
        internal SketchSegmentModel Geometry = new SketchSegmentModel();
        internal double Parameter;
        internal DrawingPreviewPoint Direction;
        private bool _construction;
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            if (ar.IsStoring && Geometry.IsConstruction != _construction)
            {
                if ((KindFlags & 5) != 0)
                    throw new NotSupportedException("Construction edits on specialized native line variants are unverified.");
                KindFlags = (ushort)((KindFlags & ~2) | (Geometry.IsConstruction ? 2 : 0));
            }
            ar.U16(ref KindFlags);
            SerializeBase(ar, ownerIs3D);
            if ((Entity.Flags56 & 0x200) != 0) DrawingPreviewRecordModel.Point(ar, ref Direction);
            ar.U16(ref StateE8);
            // sgSketch's ordinary/construction dispatch calls 0x4C41C740
            // after the payload, overriding bit 8. Specialized variants keep it.
            if (ar.IsLoading && (KindFlags & 5) == 0)
                _construction = Geometry.IsConstruction = (KindFlags & 2) != 0;
        }
        internal void SerializeBase(MfcArchive ar, bool ownerIs3D)
        {
            // sgEntHandle::isConstructionGeom (0x4C5F4C40) reads +0x54 bit 8.
            // Preserve original wire state unless the geometry property changes.
            if (ar.IsStoring && Geometry.IsConstruction != _construction)
                Entity.Flags54 = (ushort)((Entity.Flags54 & ~8) | (Geometry.IsConstruction ? 8 : 0));
            if (ar.IsStoring) Entity.Id = Geometry.Id;
            Entity.Serialize(ar, ownerIs3D); Geometry.Id = Entity.Id;
            if (ar.IsLoading) _construction = Geometry.IsConstruction = (Entity.Flags54 & 8) != 0;
            else _construction = Geometry.IsConstruction;
            int start = Geometry.StartPointIndex, end = Geometry.EndPointIndex;
            Endpoint(ar, ref start, ref StartExtended); Endpoint(ar, ref end, ref EndExtended);
            Geometry.StartPointIndex = start; Geometry.EndPointIndex = end;
            ar.U16(ref StateD0); ar.U16(ref StateD4); ar.F64(ref Parameter);
        }
        internal static void Endpoint(MfcArchive ar, ref int index, ref bool extended)
        {
            ushort word = extended || unchecked((uint)index) >= 0xFFFF ? (ushort)0xFFFF : (ushort)index;
            ar.U16(ref word);
            if (ar.IsLoading) extended = word == 0xFFFF;
            if (word == 0xFFFF) ar.I32(ref index);
            else if (ar.IsLoading) index = word;
        }
    }

    // sgSketch::Serialize 0x4C5BA790, first table only. The remaining entity,
    // relation and solver tables must follow before this is a full root codec.
    internal void SerializeNativePointTable(MfcArchive ar, bool ownerIs3D)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch point table.");
        int count = NativePoints.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) NativePoints = new NativePoint[count];
        EntityCount = checked((uint)count);
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) NativePoints[i] = new NativePoint();
            NativePoints[i].Geometry.TableIndex = i;
            NativePoints[i].Serialize(ar, ownerIs3D);
        }
    }

    internal sealed class NativeEntityFields
    {
        internal uint Id;
        internal int StateC, State20, State50;
        internal NativeDrawingEntityVisualModel Visual = new NativeDrawingEntityVisualModel();
        internal ushort InheritedStyle, State30, State34, State38, Flags54, Flags56;
        internal NativeDrawingLineWidthModel Width = new NativeDrawingLineWidthModel { RestoreType = 2 };
        internal NativeDrawingObjectGraph.Node? ExternalEntity, Group;
        internal byte HasLabel;
        internal string? Label;
        internal double State68;
        internal uint State74, State78;

        // Shared entity payload 0x4C5AC710. Owner vtable+0x1EC0 determines
        // the early visual-properties branch; the 2D profile vtable returns 0.
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            ar.U32(ref Id); ar.I32(ref StateC);
            if (!ownerIs3D) Visual.Serialize(ar);
            ar.I32(ref State20); ar.U16(ref InheritedStyle);
            if (InheritedStyle == 0)
            {
                ar.U16(ref State30); ar.U16(ref State34); ar.U16(ref State38); Width.Serialize(ar);
            }
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref ExternalEntity); ar.U16(ref Flags54); ar.U16(ref Flags56);
            // Native 0x4C41B970 rejects only the combined low-bit state 3.
            if ((Flags54 & 3) == 3) throw new InvalidDataException("Invalid native sketch entity state.");
            ar.I32(ref State50);
            if (InheritedStyle == 0) graph.Object(ar, ref Group);
            ar.Byte(ref HasLabel); if (HasLabel != 0) DrawingPreviewRecordModel.String(ar, ref Label);
            ar.F64(ref State68);
            if (ar.DocumentVersion >= 0x27AC) ar.U32(ref State74);
            if (ar.DocumentVersion >= 0x27C5) ar.U32(ref State78);
        }
    }

    internal sealed class NativePoint
    {
        internal ushort Kind, Flags, StateE4, StateE8, StateEC, HasJet, StateF0;
        internal NativeEntityFields Entity = new NativeEntityFields();
        internal SketchPointModel Geometry = new SketchPointModel();
        internal NativeDrawingObjectGraph.Node? FirstHandle, SecondHandle;
        internal int State108;
        internal DrawingPreviewPoint AuxiliaryPoint1, AuxiliaryPoint2;
        internal double JetParameter;
        internal DrawingPreviewPoint[] JetVectors = Array.Empty<DrawingPreviewPoint>();

        // Table kinds 0/1/2 all call 0x4C5B89B0, with different native
        // construction types. They share this wire payload, not MFC tags.
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            ar.U16(ref Kind);
            if (Kind > 2) throw new NotSupportedException("Unverified native sketch point-table kind.");
            if (ar.IsStoring) Entity.Id = Geometry.Id;
            Entity.Serialize(ar, ownerIs3D); Geometry.Id = Entity.Id;
            ar.U16(ref Flags);
            var position = new DrawingPreviewPoint(Geometry.X, Geometry.Y, Geometry.Z);
            Coordinates(ar, ref position);
            Geometry.X = position.X; Geometry.Y = position.Y; Geometry.Z = position.Z;
            ar.U16(ref StateE4); ar.U16(ref StateE8);
            if ((Flags & 0x20) == 0)
            {
                var graph = NativeDrawingModelFactory.Graph(ar);
                graph.Object(ar, ref FirstHandle); graph.Object(ar, ref SecondHandle);
            }
            ar.U16(ref StateEC);
            if ((Flags & 0x20) == 0)
            {
                ar.I32(ref State108); Coordinates(ar, ref AuxiliaryPoint1); Coordinates(ar, ref AuxiliaryPoint2);
            }
            ar.U16(ref HasJet);
            if (HasJet != 0)
            {
                // sldgcu gcJet restore/save 0x43F02330 / 0x43F023E0:
                // one f64, an i32 count, then ordinary 3D mgVector values.
                ar.F64(ref JetParameter);
                int count = JetVectors.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                if (ar.IsLoading)
                {
                    if (count > ar.Remaining / 24) throw new InvalidDataException("Invalid native sketch point jet count.");
                    JetVectors = new DrawingPreviewPoint[count];
                }
                for (int i = 0; i < count; i++) DrawingPreviewRecordModel.Point(ar, ref JetVectors[i]);
            }
            ar.U16(ref StateF0);
        }

        private void Coordinates(MfcArchive ar, ref DrawingPreviewPoint value)
        {
            bool twoDimensional = (Flags & 0x10) != 0;
            if (ar.IsStoring && twoDimensional && value.Z != 0)
                throw new InvalidDataException("A native 2D sketch point cannot store a nonzero Z coordinate.");
            double x = value.X, y = value.Y, z = twoDimensional ? 0 : value.Z;
            ar.F64(ref x); ar.F64(ref y); if (!twoDimensional) ar.F64(ref z);
            if (ar.IsLoading) value = new DrawingPreviewPoint(x, y, z);
        }
    }
}
