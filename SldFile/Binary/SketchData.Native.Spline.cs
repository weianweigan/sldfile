using System;
using System.IO;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeSpline[] NativeSplines = Array.Empty<NativeSpline>();

    internal void SerializeNativeSplineTable(MfcArchive ar, bool ownerIs3D)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native spline table.");
        int count = NativeSplines.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) NativeSplines = new NativeSpline[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) NativeSplines[i] = new NativeSpline();
            var row = NativeSplines[i]; row.Serialize(ar, ownerIs3D);
            if (ar.IsLoading)
            {
                row.Geometry.StartPoint = PointAt(row.Geometry.StartPointIndex);
                row.Geometry.EndPoint = PointAt(row.Geometry.EndPointIndex);
            }
        }
    }

    // sgSketch's eighth table, native shared payload 0x4C5C3F70.
    // This extends the existing sgSketch model and projects through the existing
    // SketchSegmentModel; it is not a second sgSpline MFC root implementation.
    internal sealed class NativeSpline
    {
        internal ushort KindFlags, State2FC, State170, StateF0, State2F8, State318, State31C, StateD8, State340;
        internal int State32C, StateF4, State154, State314, State174, StateAfterSources, State190;
        internal double StateF8, State100, State180, StateD8Value, State188;
        internal NativeLine Base = new NativeLine();
        internal SketchSegmentModel Geometry => Base.Geometry;
        internal NativeDrawingObjectGraph.Node? ControlPointList;
        internal DrawingPreviewPoint Vector118, Vector130;
        internal double[] Values148 = Array.Empty<double>();
        internal DrawingPreviewPoint[] FitPoints = Array.Empty<DrawingPreviewPoint>();
        internal Tangent[] Tangents = Array.Empty<Tangent>();
        internal Interpolation? FirstInterpolation, SecondInterpolation;
        internal FixedPoint[] FixedPoints = Array.Empty<FixedPoint>();
        internal ControlCurve? Curve;

        internal sealed class Tangent
        {
            internal int Key;
            internal ushort Type;
            internal DrawingPreviewPoint Vector;
            internal double Value;
        }
        internal sealed class FixedPoint
        {
            internal int Key;
            internal NativeDrawingObjectGraph.Node? Data;
        }

        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            ar.U16(ref KindFlags);
            // Native specialized/control-polygon variants may dispatch through
            // additional helpers. Do not reinterpret their discriminator as an
            // ordinary spline, even if a short fixture happens to fit.
            if ((KindFlags & ~8) != 0) throw new NotSupportedException("Specialized native spline table variant is not decoded yet.");
            Base.SerializeBase(ar, ownerIs3D); Geometry.Kind = SketchSegmentKind.Spline;
            ar.I32(ref State32C); ar.U16(ref State2FC);
            var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref ControlPointList);
            ar.U16(ref State170); ar.U16(ref StateF0); ar.I32(ref StateF4); ar.F64(ref StateF8); ar.F64(ref State100);
            ar.F64(ref State180); ar.I32(ref State154);
            DrawingPreviewRecordModel.Point(ar, ref Vector118); DrawingPreviewRecordModel.Point(ar, ref Vector130);
            ar.U16(ref State2F8); Doubles(ar, ref Values148);
            if ((State32C & 1) == 0) Points(ar, ref FitPoints);
            int count = Tangents.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) Tangents = new Tangent[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) Tangents[i] = new Tangent();
                ar.I32(ref Tangents[i].Key); ar.U16(ref Tangents[i].Type);
                DrawingPreviewRecordModel.Point(ar, ref Tangents[i].Vector);
                if (Tangents[i].Type == 2) ar.F64(ref Tangents[i].Value);
            }
            uint interpolationCount = FirstInterpolation == null ? uint.MaxValue : (uint)FirstInterpolation.Parameters.Length;
            ar.U32(ref interpolationCount);
            if (interpolationCount != uint.MaxValue)
            {
                if (ar.IsLoading) FirstInterpolation = new Interpolation();
                FirstInterpolation!.Serialize(ar, checked((int)interpolationCount), false);
            }
            else if (ar.IsLoading) FirstInterpolation = null;
            ar.I32(ref State314); ar.U16(ref State318); ar.U16(ref State31C);
            if (State314 != 0)
            {
                if (FitPoints.Length == 0)
                    throw new NotSupportedException("Native spline interpolation requires unverified virtual variant dispatch.");
                if (ar.IsLoading) SecondInterpolation = new Interpolation();
                if (SecondInterpolation == null) throw new InvalidDataException("Missing native spline second interpolation.");
                count = SecondInterpolation.Parameters.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                SecondInterpolation.Serialize(ar, count, true);
            }
            ar.I32(ref State174);
            if ((State32C & 4) != 0) throw new NotSupportedException("Native spline source/equation transform payload is not decoded yet.");
            ar.I32(ref StateAfterSources);
            // The native +0x88 virtual is a list-kind query, NOT an IsEmpty
            // test: sgSplineList returns -2 and modifSplineList returns 1.
            // A nonempty modif list therefore has no StateD8 word here.
            if (ControlPointList != null && ControlPointList.ClassName != "sgSplineList_c"
                && ControlPointList.ClassName != "modifSplineList_c")
            {
                if (ControlPointList.ClassName != "cptsSplineList_c")
                    throw new NotSupportedException("Native spline list-kind dispatch is not decoded for this runtime class.");
                // cptsSplineList vtable 0x4CEEDDB8+0x88 returns zero.
                // 0x4C5C8DE0 serializes explicit B-curve data when bit 1 is set.
                if ((State32C & 1) != 0)
                {
                    if (ar.IsLoading) Curve = new ControlCurve();
                    if (Curve == null) throw new InvalidDataException("Missing native spline control curve.");
                    Curve.Serialize(ar);
                }
                ar.U16(ref StateD8); if (StateD8 != 0) ar.F64(ref StateD8Value);
            }
            ar.F64(ref State188);
            count = FixedPoints.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) FixedPoints = new FixedPoint[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) FixedPoints[i] = new FixedPoint();
                ar.I32(ref FixedPoints[i].Key); graph.Object(ar, ref FixedPoints[i].Data);
            }
            ar.U16(ref State340);
            ushort hasKernel = 0; ar.U16(ref hasKernel);
            if (hasKernel != 0) throw new NotSupportedException("Native spline kernel body is not decoded yet.");
            ar.I32(ref State190);
        }

        internal sealed class Interpolation
        {
            internal int Dimension;
            internal double Start, End;
            internal double[] Parameters = Array.Empty<double>(), Values = Array.Empty<double>();
            internal ushort[] Flags = Array.Empty<ushort>();
            internal int[] Indices = Array.Empty<int>();
            internal void Serialize(MfcArchive ar, int count, bool parametersOnly)
            {
                ar.I32(ref Dimension); ar.F64(ref Start); ar.F64(ref End);
                FixedDoubles(ar, ref Parameters, count);
                if (parametersOnly) return;
                if (ar.IsLoading)
                {
                    if (count > ar.Remaining / 6) throw new InvalidDataException("Invalid native spline interpolation count.");
                    Flags = new ushort[count]; Indices = new int[count];
                }
                else if (Flags.Length != count || Indices.Length != count) throw new InvalidDataException("Inconsistent spline interpolation topology.");
                for (int i = 0; i < count; i++) ar.U16(ref Flags[i]);
                for (int i = 0; i < count; i++) ar.I32(ref Indices[i]);
                if (Dimension < 0) throw new InvalidDataException("Invalid native spline interpolation dimension.");
                FixedDoubles(ar, ref Values, checked(count * Dimension));
            }
        }

        // Helper 0x4C5C8DE0, modern non-DB archive: four u16 values followed
        // by dimension*count control coordinates and count+(mode?1:order)
        // knot values. The native make_gcBcurve call separates the two arrays
        // at dimension*count, without fitting/resampling coordinates.
        internal sealed class ControlCurve
        {
            internal ushort Dimension, Order, PointCount, ParameterMode;
            internal double[] Coordinates = Array.Empty<double>(), Knots = Array.Empty<double>();
            internal void Serialize(MfcArchive ar)
            {
                ar.U16(ref Dimension); ar.U16(ref Order); ar.U16(ref PointCount); ar.U16(ref ParameterMode);
                if (Dimension == 0)
                {
                    if (ar.IsLoading) { Coordinates = Array.Empty<double>(); Knots = Array.Empty<double>(); }
                    if (Order != 0 || PointCount != 0 || ParameterMode != 0
                        || Coordinates.Length != 0 || Knots.Length != 0)
                        throw new InvalidDataException("Invalid empty native spline control curve.");
                    return;
                }
                if (Dimension != 3 && Dimension != 4) throw new InvalidDataException("Invalid native spline control dimension.");
                FixedDoubles(ar, ref Coordinates, Dimension * PointCount);
                FixedDoubles(ar, ref Knots, PointCount + (ParameterMode != 0 ? 1 : Order));
            }
        }

        private static void Doubles(MfcArchive ar, ref double[] values)
        {
            int count = values.Length; NativeDrawingDisplayArchive.Count(ar, ref count); FixedDoubles(ar, ref values, count);
        }
        private static void FixedDoubles(MfcArchive ar, ref double[] values, int count)
        {
            if (ar.IsLoading)
            {
                if (count < 0 || count > ar.Remaining / 8) throw new InvalidDataException("Invalid native spline double count.");
                values = new double[count];
            }
            else if (values.Length != count) throw new InvalidDataException("Inconsistent native spline array length.");
            for (int i = 0; i < count; i++) ar.F64(ref values[i]);
        }
        private static void Points(MfcArchive ar, ref DrawingPreviewPoint[] values)
        {
            int count = values.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading)
            {
                if (count > ar.Remaining / 24) throw new InvalidDataException("Invalid native spline point count.");
                values = new DrawingPreviewPoint[count];
            }
            for (int i = 0; i < count; i++) DrawingPreviewRecordModel.Point(ar, ref values[i]);
        }
    }
}
