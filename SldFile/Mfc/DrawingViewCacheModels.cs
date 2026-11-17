using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile.Drawing;

namespace SldFile.Mfc;

internal sealed class DrawingViewCacheArchive : IMfcSerializable
{
    internal sealed class Fragment
    {
        internal int Offset, ViewIndex;
        internal IMfcSerializable Model = null!;
        internal string Owner = string.Empty;
        internal bool ViewLocalCoordinates, Hidden;
    }
    private byte[] _source = Array.Empty<byte>();
    internal readonly List<Fragment> Fragments = new List<Fragment>();
    internal bool IsDefinition;
    internal bool PopulateNativeFragments;
    internal string[] ViewNames = Array.Empty<string>();
    internal NativeDrawingVbArchive? NativeBuckets;
    internal DrSheetMfcModel[] NativeSheets = Array.Empty<DrSheetMfcModel>();
    internal string? DecodeFailure;

    internal static DrawingViewCacheArchive Read(byte[]? bytes, bool definition, string[] names, int documentVersion = 0, bool populateNativeFragments = false)
    {
        var model = new DrawingViewCacheArchive { IsDefinition = definition, ViewNames = names, PopulateNativeFragments = populateNativeFragments };
        using var ar = MfcArchive.CreateReader(bytes ?? Array.Empty<byte>());
        ar.DocumentVersion = documentVersion; model.Serialize(ar);
        return model;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (NativeBuckets != null) { NativeBuckets.Serialize(ar); return; }
            ar.WriteBytes(_source);
            return;
        }
        _source = ar.ReadRemainingBytes();
        Fragments.Clear();
        NativeBuckets = null;
        NativeSheets = Array.Empty<DrSheetMfcModel>();
        DecodeFailure = null;
        if (IsDefinition && _source.Length >= 36 && ar.DocumentVersion >= 5000)
        {
            // Ordinary Definition: 36-byte document preamble followed by the
            // drawing root. Only its common data and resolved feature list are
            // needed here; retain the unparsed manager/defaults tail verbatim.
            using var input = MfcArchive.CreateReader(_source);
            input.DocumentVersion = ar.DocumentVersion;
            input.Position = 36;
            input.Context.NativeSketchIncludesOwnerProfile = false;
            input.Context.NativeAnnotationReferenceIsExternal = false;
            var prefix = new DefinitionFeaturePrefix();
            NativeDrawingObjectGraph.Node? root = null;
            try
            {
                NativeDrawingModelFactory.CreateGraph((name, schema) =>
                    name == "moDrawing_c" && schema == 1 ? prefix : null).Object(input, ref root);
                if (root?.Payload != prefix) return;
                var sheets = prefix.Features.Values.Select(n => n?.Payload).OfType<DrSheetMfcModel>().ToArray();
                NativeSheets = sheets;
                if (sheets.Length != 1 || sheets[0].NativeSheet == null) return;
                foreach (var view in sheets[0].NativeSheet!.Feature.Feature.ResolvedChildren.Values
                    .Select(n => n?.Payload).OfType<DrViewMfcModel>())
                {
                    var state = view.NativeView!;
                    string name = (state.Feature.Feature.Lightweight.Name?.Payload as NativeDrawingStringHandleModel)?.Value ?? string.Empty;
                    if (name.Length == 0) continue;
                    Fragments.Add(new Fragment { Owner = name, Model = view });
                }
            }
            catch (Exception error) when (error is NotSupportedException || error is InvalidDataException || error is EndOfStreamException)
            {
                DecodeFailure = $"Definition at {input.Position}: {error.Message}";
                Fragments.Clear();
            }
            return;
        }
        if (!IsDefinition && _source.Length != 0 && ar.DocumentVersion >= 5000)
        {
            using var input = MfcArchive.CreateReader(_source);
            input.DocumentVersion = ar.DocumentVersion;
            var native = new NativeDrawingVbArchive();
            try
            {
                native.Serialize(input);
                if (input.Remaining != 0) throw new InvalidDataException("Unconsumed native VBLists data.");
                NativeBuckets = native;
                if (PopulateNativeFragments)
                {
                    Fragments.AddRange(ExtractNativeFragments());
                }
            }
            catch (Exception error) when (error is NotSupportedException || error is InvalidDataException || error is EndOfStreamException)
            {
                DecodeFailure = $"VBLists at {input.Position}: {error.Message}";
                // Unsupported layouts retain the complete source; never resume
                // at another apparent tag inside an unrecognized object.
            }
        }
        // Never locate placements by numeric plausibility or nearest view strings.
    }

    private sealed class DefinitionFeaturePrefix : IMfcSerializable
    {
        internal NativeDrawingModelCommonModel Common = new NativeDrawingModelCommonModel();
        internal NativeDrawingNodeListModel Features = new NativeDrawingNodeListModel();
        public void Serialize(MfcArchive ar) { Common.Serialize(ar); Features.Serialize(ar); }
    }

    internal IReadOnlyList<Fragment> ExtractNativeFragments()
    {
        if (NativeBuckets == null) return Array.Empty<Fragment>();
        var list = new List<Fragment>();
        for (int vIdx = 0; vIdx < NativeBuckets.Views.Length; vIdx++)
        {
            if (NativeBuckets.Views[vIdx]?.Payload is not NativeDrawingViewBucketModel vb) continue;
            string owner = vIdx < ViewNames.Length && !string.IsNullOrEmpty(ViewNames[vIdx])
                ? ViewNames[vIdx] : $"ViewBucket_{vIdx}";
            void AppendBucket(NativeDrawingBucketModel bucket)
            {
                void Append(NativeDrawingBucketPolyModel poly, bool hidden)
                {
                    if (poly.PointCount < 2) return;
                    // Keep the original wire object: curve parameters, native flags,
                    // line overrides and entity identity must not be flattened away.
                    list.Add(new Fragment { Offset = poly.SourceOffset, Owner = owner, ViewIndex = vIdx,
                        ViewLocalCoordinates = vb.HasFirstTransform != 0, Hidden = hidden, Model = poly });
                }
                for (int set = 0; set < bucket.PolygonSets.Length; set++)
                    foreach (var poly in bucket.PolygonSets[set] ?? Array.Empty<NativeDrawingBucketPolyModel>())
                        Append(poly, set == 1 || set == 3 || set == 5);
                foreach (var map in bucket.PolygonMaps)
                    foreach (var entry in map ?? Array.Empty<NativeDrawingBucketModel.PolygonEntry>())
                        foreach (var poly in entry.Polygons) Append(poly, false);
                foreach (var poly in bucket.ExtraPolygons) Append(poly, false);
            }
            AppendBucket(vb.Bucket);
            if (vb.HasDetailBucket != 0) AppendBucket(vb.DetailBucket);
        }
        return list;
    }
}
