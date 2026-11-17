using System;
using System.IO;
using SldFile.Binary;

namespace SldFile.Mfc;

public sealed partial class SketchFeatureModel
{
    internal NativeProfileState? NativeProfile;

    // Actual moProfileFeature_c vtable 0x4D0FBBA0 -> 0x4BB6B560.
    // Keep the existing aggregate format separate and reuse its owning type.
    internal sealed class NativeProfileState
    {
        internal NativeDrawingModelFeatureModel Feature = new NativeDrawingModelFeatureModel();
        internal int State388, State3D4, State11C, State118, State424;
        internal uint State2B0, State3D8, State2A0;
        internal ushort State3B8, State100, HasTransform;
        internal NativeDrawingObjectGraph.Node? Objects, Sketch, DerivedProfile, Chooser, Line, Plane, ProfileReference;
        internal NativeDrawingObjectGraph.Node?[] Chains = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal Contour[] Contours = Array.Empty<Contour>();
        internal BodyState Body = new BodyState();
        internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel(), OptionalTransform = new NativeDrawingTransformModel();
        internal NativeDrawingObjectGraph.Node?[] Objects128 = Array.Empty<NativeDrawingObjectGraph.Node?>(), Objects148 = Array.Empty<NativeDrawingObjectGraph.Node?>();

        internal void Serialize(MfcArchive ar)
        {
            if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native profile layout.");
            if (!ar.Context.NativeSketchIncludesOwnerProfile.HasValue)
                throw new InvalidOperationException("Native profile requires explicit sketch archive context.");
            if (ar.Context.NativeSketchIncludesOwnerProfile.Value)
                throw new NotSupportedException("Native profile archive +834 mode is not decoded.");
            var graph = NativeDrawingModelFactory.Graph(ar);
            Feature.Serialize(ar); ar.I32(ref State388); graph.Object(ar, ref Objects); ar.I32(ref State3D4);
            if (State3D4 != 0)
            {
                graph.Object(ar, ref DerivedProfile); graph.Object(ar, ref Chooser);
                ar.U16(ref State3B8); graph.Object(ar, ref Line); SerializeContours(ar); return;
            }
            // The native 2D profile owner returns zero at vtable+0x1EC0.
            // +834 belongs to archive context and must be supplied explicitly.
            bool? previousOwner = ar.Context.NativeSketchOwnerIs3D;
            ar.Context.NativeSketchOwnerIs3D = false;
            try { graph.Object(ar, ref Sketch); }
            finally { ar.Context.NativeSketchOwnerIs3D = previousOwner; }
            // Current versions use 0x4BB7CD00's i32-count chain set.
            ar.U32(ref State2B0); ObjectsI32(ar, ref Chains);
            Body.Serialize(ar); SerializeContours(ar); graph.Object(ar, ref Line);
            // Ordinary context has no condition-1 optional-plane marker.
            graph.Object(ar, ref Plane); Transform.Serialize(ar);
            ar.U16(ref HasTransform); if (HasTransform != 0) OptionalTransform.Serialize(ar);
            ar.U16(ref State100); graph.Object(ar, ref ProfileReference);
            ar.I32(ref State11C); ar.I32(ref State118); ar.U32(ref State2A0); ar.U16(ref State3B8);
            graph.Object(ar, ref Chooser);
            // Constructor 0x4C10CBE0 establishes +128 and +148 as suObArray.
            NativeDrawingObjectArray.Serialize(ar, ref Objects128); NativeDrawingObjectArray.Serialize(ar, ref Objects148);
            if (ar.DocumentVersion > 0x2B91) ar.I32(ref State424);
        }

        private static void ObjectsI32(MfcArchive ar, ref NativeDrawingObjectGraph.Node?[] objects)
        {
            int count = objects.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) objects = new NativeDrawingObjectGraph.Node?[count];
            var graph = NativeDrawingModelFactory.Graph(ar);
            for (int i = 0; i < count; i++) graph.Object(ar, ref objects[i]);
        }

        // 0x4BB7C910: U32 state, CArchive count, tagged moOneContour_c key,
        // then a u16-count U32 array. These are not aggregate contour IDs.
        internal sealed class Contour
        {
            internal NativeDrawingObjectGraph.Node? Object;
            internal uint[] Indices = Array.Empty<uint>();
        }
        private void SerializeContours(MfcArchive ar)
        {
            ar.U32(ref State3D8); uint count = (uint)Contours.Length; ar.Count(ref count);
            if (count > int.MaxValue || ar.IsLoading && count > ar.Remaining / 4)
                throw new InvalidDataException("Invalid native profile contour count.");
            if (ar.IsLoading) Contours = new Contour[count];
            var graph = NativeDrawingModelFactory.Graph(ar);
            for (int i = 0; i < Contours.Length; i++)
            {
                if (ar.IsLoading) Contours[i] = new Contour();
                var contour = Contours[i]; graph.Object(ar, ref contour.Object);
                if (contour.Indices.Length > ushort.MaxValue) throw new InvalidDataException("Too many native contour indices.");
                ushort length = (ushort)contour.Indices.Length; ar.U16(ref length);
                if (ar.IsLoading)
                {
                    if (length > ar.Remaining / 4) throw new InvalidDataException("Invalid native contour index count.");
                    contour.Indices = new uint[length];
                }
                for (int j = 0; j < length; j++) ar.U32(ref contour.Indices[j]);
            }
        }

        // 0x4BB7C170, ordinary >=5000 context, no native kernel body.
        internal sealed class BodyState
        {
            internal int State10;
            internal byte Marker;
            internal NativeDrawingFacetModel.Pair[] Map = Array.Empty<NativeDrawingFacetModel.Pair>();
            internal NativeDrawingObjectGraph.Node?[] Objects = Array.Empty<NativeDrawingObjectGraph.Node?>();
            internal SketchData.NativeDynamicI32Array Indices = new SketchData.NativeDynamicI32Array();
            internal void Serialize(MfcArchive ar)
            {
                ar.I32(ref State10); ar.Byte(ref Marker);
                // 0x4C290990 returns immediately for nonzero marker. Zero
                // invokes the kernel reader; no guessed byte count or skip.
                if (Marker == 0) throw new NotSupportedException("Native profile kernel-body layout is not decoded.");
                int count = Map.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                if (ar.IsLoading) Map = new NativeDrawingFacetModel.Pair[count];
                for (int i = 0; i < count; i++)
                {
                    if (ar.IsLoading) Map[i] = new NativeDrawingFacetModel.Pair();
                    ar.I32(ref Map[i].Key); ar.I32(ref Map[i].Value);
                }
                NativeDrawingObjectArray.Serialize(ar, ref Objects); Indices.Serialize(ar);
            }
        }
    }
}
