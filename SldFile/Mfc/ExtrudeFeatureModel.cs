using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;



/// <summary>
/// Unified bi-directional model for moExtrusion_c (Boss Extrude) and moICE_c (Cut Extrude).
/// Implements <see cref="IMfcSerializable"/> to guarantee symmetric reading, writing, and roundtrip fidelity.
/// </summary>
public sealed class ExtrudeFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string BossClassName = "moExtrusion_c";
    public const string CutClassName = "moICE_c";

    public const uint BossTypeId = 0x4Fu;
    public const uint CutTypeId = 0x56u;

    public string ClassName => EffectiveClassName;

    public string FeatureName { get; set; } = "凸台-拉伸1";
    public double Version { get; set; } = 2.0;
    public uint TypeId { get => EffectiveTypeId; set { } }

    public double Depth { get; set; } = 0.0;
    public uint EndCondition { get; set; } = 0; // Blind
    public bool IsCut { get; set; } = false;
    public bool Merge { get; set; } = true;
    public bool BothDirections { get; set; } = false;
    public double Direction2Depth { get; set; } = 0.0;
    public double DraftAngle { get; set; } = 0.0;
    public bool DraftOutward { get; set; } = false;
    public string? SketchName { get; set; } = string.Empty;
    public List<uint> EntityIndices { get; set; } = new();

    // Selection and Reference sub-entities
    public EndConditionEntityReference? EndConditionEntity { get; set; }
    public BodyScopeData? BodyScope { get; set; }
    public DirectionReference? DirectionReference { get; set; }
    public double DepthDirection2 { get => Direction2Depth; set => Direction2Depth = value; }
    public uint EndConditionDirection2 { get; set; }
    public bool ReverseDirection { get; set; }
    public double DraftAngleDirection2 { get; set; }
    public bool DraftOutwardDirection2 { get; set; }
    public uint FromType { get; set; }
    public double FromOffsetDistance { get; set; }
    public bool FromOffsetReverse { get; set; }
    public bool FlipSideToCut { get; set; }
    public bool NormalCut { get; set; }
    public bool OptimizeGeometry { get; set; }
    public bool IsThinFeature { get; set; }
    public uint ThinWallType { get; set; }
    public double WallThickness { get; set; }
    public double WallThicknessDirection2 { get; set; }
    public bool CapEnds { get; set; }
    public double CapThickness { get; set; }
    public bool LinkToThickness { get; set; }
    public bool DraftWhileExtruding { get; set; }
    public bool DraftWhileExtrudingDirection2 { get; set; }
    public bool TranslateSurface { get; set; }
    public bool TranslateSurfaceDirection2 { get; set; }
    public bool ReverseOffset { get; set; }
    public bool ReverseOffsetDirection2 { get; set; }
    public bool AutoFilletCorners { get; set; }
    public double AutoFilletRadius { get; set; }
    public bool IsBaseExtrude { get; set; }

    public bool AutoSelect { get; set; } = true;
    public bool FeatureScope { get; set; }

    public bool AssemblyFeatureScope { get; set; }
    public bool AutoSelectComponents { get; set; }
    public bool PropagateFeatureToParts { get; set; }

    public SketchSelectionMode SketchSelectionMode { get; set; } = SketchSelectionMode.Unknown;

    public IReadOnlyList<SketchContourReference> SelectedContours { get; set; } = Array.Empty<SketchContourReference>();
    public IReadOnlyList<SketchRegionReference> SelectedRegions { get; set; } = Array.Empty<SketchRegionReference>();

    public IReadOnlyList<SketchContourReference> AvailableSketchContours { get; set; } = Array.Empty<SketchContourReference>();
    public IReadOnlyList<SketchRegionReference> AvailableSketchRegions { get; set; } = Array.Empty<SketchRegionReference>();

    /// <summary>
    /// 缁熶竴灏哄鍙傛暟椹卞姩灞傛ā鍨?(moLengthParameter_c).
    /// </summary>
    public MoLengthParameterModel Parameter => new MoLengthParameterModel("Depth@" + FeatureName, Depth);

    /// <summary>
    /// 缁熶竴灏哄鏄剧ず鍑犱綍甯冨眬灞傛ā鍨?(moDisplayDistanceDim_c).
    /// </summary>
    public MoDisplayDistanceDimModel DisplayDistanceDimension => new MoDisplayDistanceDimModel();

    /// <summary>
    /// 缁熶竴灏哄瀵硅薄妯″瀷.
    /// </summary>
    public DimensionModel Dimension => new DimensionModel(Parameter, DisplayDistanceDimension);

    /// <summary>
    /// 鐗瑰緛鎵€鍖呭惈鐨勭粺涓€灏哄鍒楄〃.
    /// </summary>
    public IReadOnlyList<DimensionModel> Dimensions
    {
        get
        {
            var list = new List<DimensionModel> { Dimension };
            if (BothDirections && Direction2Depth > 0)
            {
                var p2 = new MoLengthParameterModel("Depth2@" + FeatureName, Direction2Depth);
                var d2 = new MoDisplayDistanceDimModel();
                list.Add(new DimensionModel(p2, d2));
            }
            if (DraftAngle > 0)
            {
                var pa = new MoAngleParameterModel("Draft@" + FeatureName, DraftAngle);
                var da = new MoDisplayAngularDimModel();
                list.Add(new DimensionModel(pa, da));
            }
            return list.AsReadOnly();
        }
    }

    public MfcContourSelection? ContourSelection
    {
        get
        {
            if (SelectedContours.Count == 0 && SelectedRegions.Count == 0 && SketchSelectionMode == SketchSelectionMode.Unknown)
                return null;
            return new MfcContourSelection
            {
                SelectionMode = SketchSelectionMode,
                Contours = SelectedContours.Select(c => new MfcContourItem(c)).ToList(),
                Regions = SelectedRegions.Select(r => new MfcRegionItem(r)).ToList()
            };
        }
        set
        {
            if (value == null)
            {
                SketchSelectionMode = SketchSelectionMode.Unknown;
                SelectedContours = Array.Empty<SketchContourReference>();
                SelectedRegions = Array.Empty<SketchRegionReference>();
            }
            else
            {
                SketchSelectionMode = value.SelectionMode;
                SelectedContours = value.Contours.Select(c => c.ToDefinition()).ToArray();
                SelectedRegions = value.Regions.Select(r => r.ToDefinition()).ToArray();
            }
        }
    }

    public double GetDepth(bool forward) => forward ? Depth : DepthDirection2;
    public void SetDepth(bool forward, double depth) { if (forward) Depth = depth; else DepthDirection2 = depth; }
    public uint GetEndCondition(bool forward) => forward ? EndCondition : EndConditionDirection2;
    public void SetEndCondition(bool forward, uint condition) { if (forward) EndCondition = condition; else EndConditionDirection2 = condition; }
    public double GetDraftAngle(bool forward) => forward ? DraftAngle : DraftAngleDirection2;
    public void SetDraftAngle(bool forward, double angle) { if (forward) DraftAngle = angle; else DraftAngleDirection2 = angle; }
    public bool GetDraftOutward(bool forward) => forward ? DraftOutward : DraftOutwardDirection2;
    public void SetDraftOutward(bool forward, bool outward) { if (forward) DraftOutward = outward; else DraftOutwardDirection2 = outward; }
    public bool GetDraftWhileExtruding(bool forward) => forward ? DraftWhileExtruding : DraftWhileExtrudingDirection2;
    public void SetDraftWhileExtruding(bool forward, bool draft) { if (forward) DraftWhileExtruding = draft; else DraftWhileExtrudingDirection2 = draft; }
    public double GetWallThickness(bool forward) => forward ? WallThickness : WallThicknessDirection2;
    public void SetWallThickness(bool forward, double thickness) { if (forward) WallThickness = thickness; else WallThicknessDirection2 = thickness; }
    public bool GetReverseOffset(bool forward) => forward ? ReverseOffset : ReverseOffsetDirection2;
    public void SetReverseOffset(bool forward, bool reverse) { if (forward) ReverseOffset = reverse; else ReverseOffsetDirection2 = reverse; }
    public bool GetTranslateSurface(bool forward) => forward ? TranslateSurface : TranslateSurfaceDirection2;
    public void SetTranslateSurface(bool forward, bool translate) { if (forward) TranslateSurface = translate; else TranslateSurfaceDirection2 = translate; }
    public bool IsBossFeature() => !IsCut;

    public string EffectiveClassName => IsThinFeature ? "moExtruThin_c" : IsCut ? CutClassName : BossClassName;
    public uint EffectiveTypeId => IsThinFeature ? 0x52u : IsCut ? CutTypeId : BossTypeId;

    public ExtrudeFeatureModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            string cls = EffectiveClassName;
            ar.ClassTagOrRef(cls, ref refTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName;
            ar.WChar(ref name);

            double v = Version;
            ar.F64(ref v);

            uint tid = EffectiveTypeId;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = string.Empty;
            ar.WChar(ref empty);

            // Serialization payload
            double d = Depth;
            ar.F64(ref d);

            uint ec = EndCondition;
            ar.U32(ref ec);

            bool cut = IsCut;
            ar.Boolean(ref cut);

            bool m = Merge;
            ar.Boolean(ref m);

            bool bd = BothDirections;
            ar.Boolean(ref bd);

            double d2 = Direction2Depth;
            ar.F64(ref d2);

            double draft = DraftAngle;
            ar.F64(ref draft);

            bool doOut = DraftOutward;
            ar.Boolean(ref doOut);

            string? skName = SketchName;
            ar.WChar(ref skName);

            // Sub-entities
            bool hasContour = (SelectedContours != null && SelectedContours.Count > 0) || (SelectedRegions != null && SelectedRegions.Count > 0);
            ar.Boolean(ref hasContour);
            if (hasContour)
            {
                var cs = new MfcContourSelection
                {
                    SelectionMode = SketchSelectionMode,
                    Contours = SelectedContours?.Select(c => new MfcContourItem(c)).ToList() ?? new List<MfcContourItem>(),
                    Regions = SelectedRegions?.Select(r => new MfcRegionItem(r)).ToList() ?? new List<MfcRegionItem>()
                };
                cs.Serialize(ar);
            }

            bool hasEndEntity = EndConditionEntity != null;
            ar.Boolean(ref hasEndEntity);
            if (hasEndEntity)
            {
                if (EndConditionEntity is MfcEndConditionEntity me)
                {
                    me.Serialize(ar);
                }
                else
                {
                    var newEndEntity = new MfcEndConditionEntity
                    {
                        EntityType = EndConditionEntity!.EntityType,
                        EntityId = EndConditionEntity.EntityId,
                        SurfaceName = EndConditionEntity.SurfaceName,
                        FaceCount = EndConditionEntity.FaceCount
                    };
                    newEndEntity.Serialize(ar);
                }
            }

            bool hasBodyScope = BodyScope != null;
            ar.Boolean(ref hasBodyScope);
            if (hasBodyScope)
            {
                if (BodyScope is MfcBodyScope bs)
                {
                    bs.Serialize(ar);
                }
                else
                {
                    var b = new MfcBodyScope
                    {
                        AutoSelect = BodyScope!.AutoSelect,
                        Flags = BodyScope.Flags,
                        BodyIds = BodyScope.BodyIds?.ToList() ?? new List<uint>(),
                        Bodies = BodyScope.Bodies?.Select(item => new MfcEntityReferenceItem { EntityId = item.EntityId, EntityType = item.EntityType, Name = item.Name }).ToList() ?? new List<MfcEntityReferenceItem>()
                    };
                    b.Serialize(ar);
                }
            }

            bool hasDir = DirectionReference != null;
            ar.Boolean(ref hasDir);
            if (hasDir)
            {
                if (DirectionReference is MfcDirectionReference md)
                {
                    md.Serialize(ar);
                }
                else
                {
                    var newDir = new MfcDirectionReference
                    {
                        EntityType = DirectionReference!.EntityType,
                        EntityId = DirectionReference.EntityId,
                        ReferenceName = DirectionReference.ReferenceName,
                        Vector = DirectionReference.Vector
                    };
                    newDir.Serialize(ar);
                }
            }
        }
        else
        {
            var kind = ar.ReadClassTag(out string loadedName, out ushort tag, out _);
            if (kind == MfcTagKind.NewClass || kind == MfcTagKind.ClassReference)
            {
                IsCut = loadedName.Contains("ICE") || loadedName.Contains("Cut");
            }

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            if (!string.IsNullOrEmpty(name)) FeatureName = name!;

            double v = 0;
            ar.F64(ref v);
            Version = v;

            uint tid = 0;
            ar.U32(ref tid);
            TypeId = tid;

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = null;
            ar.WChar(ref empty);

            if (ar.Remaining >= 8)
            {
                double d = 0;
                ar.F64(ref d);
                Depth = d;
            }

            if (ar.Remaining >= 4)
            {
                uint ec = 0;
                ar.U32(ref ec);
                EndCondition = ec;
            }

            if (ar.Remaining >= 1)
            {
                bool cut = false;
                ar.Boolean(ref cut);
                IsCut = cut;
            }

            if (ar.Remaining >= 1)
            {
                bool m = true;
                ar.Boolean(ref m);
                Merge = m;
            }

            if (ar.Remaining >= 1)
            {
                bool bd = false;
                ar.Boolean(ref bd);
                BothDirections = bd;
            }

            if (ar.Remaining >= 8)
            {
                double d2 = 0;
                ar.F64(ref d2);
                Direction2Depth = d2;
            }

            if (ar.Remaining >= 8)
            {
                double draft = 0;
                ar.F64(ref draft);
                DraftAngle = draft;
            }

            if (ar.Remaining >= 1)
            {
                bool doOut = false;
                ar.Boolean(ref doOut);
                DraftOutward = doOut;
            }

            if (ar.Remaining >= 4)
            {
                string? sk = null;
                ar.WChar(ref sk);
                if (!string.IsNullOrEmpty(sk)) SketchName = sk!;
            }

            if (ar.Remaining >= 1)
            {
                bool hasContour = false;
                ar.Boolean(ref hasContour);
                if (hasContour)
                {
                    var cs = new MfcContourSelection();
                    cs.Serialize(ar);
                    SketchSelectionMode = cs.SelectionMode;
                    SelectedContours = cs.Contours.Select(c => c.ToDefinition()).ToArray();
                    SelectedRegions = cs.Regions.Select(r => r.ToDefinition()).ToArray();
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasEndEntity = false;
                ar.Boolean(ref hasEndEntity);
                if (hasEndEntity)
                {
                    var me = new MfcEndConditionEntity();
                    me.Serialize(ar);
                    EndConditionEntity = me;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasBodyScope = false;
                ar.Boolean(ref hasBodyScope);
                if (hasBodyScope)
                {
                    var bs = new MfcBodyScope();
                    bs.Serialize(ar);
                    BodyScope = bs;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasDir = false;
                ar.Boolean(ref hasDir);
                if (hasDir)
                {
                    var md = new MfcDirectionReference();
                    md.Serialize(ar);
                    DirectionReference = md;
                }
            }
        }
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExplicitSelectionExtrudeBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Controlled explicit-contour selection extrusion authoring on the measured
/// SelectionAuthoring dual-rectangle bases. The target stream must be the native
/// explicit-selection part stripped at its extrusion tag: the sketch prefix, the
/// chooser window and the complete trim/reference suffix are retained, while the
/// extrusion feature is absent. The suffix is captured from that window and the
/// selection block is rebuilt with the requested (measured) contour chains.
/// </summary>
internal static class ExplicitSelectionExtrudeBuilder
{
    private const string ChooserClass = "moContourChooser_c";
    private const string ExtrudeClass = "moExtrusion_c";
    private const string CutExtrudeClass = "moICE_c";

    internal static byte[] Build(
        byte[] currentStream,
        ExtrudeFeatureModel def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName,
        string? targetDocumentPath)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));
        if (def.SelectedRegions != null && def.SelectedRegions.Count > 0)
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring does not accept SelectedRegions. No stream changes have been made.");
        }
        if (!CStringHandlePathPolicy.TryNormalizeDocumentPath(targetDocumentPath, out string normalizedTarget))
        {
            throw new ArgumentException(
                "A valid absolute target document path is required for explicit contour selection authoring. " +
                "No stream changes have been made.", nameof(targetDocumentPath));
        }
        if (sketchEndOffset != currentStream.Length)
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring requires the extrusion-stripped SelectionAuthoring base " +
                "whose selection window ends at the stream end. No stream changes have been made.");
        }

        var doc = MfcDocument.Parse(currentStream);
        var chooser = FindChooser(doc, targetSketchFolder.TagOffset, sketchEndOffset);
        EnsureWindowIsLastData(doc, chooser, sketchEndOffset);
        EnsureNoExistingExtrusion(doc, chooser.TagOffset);

        var available = SelectedSketchContourParser.TryRead(doc, chooser);
        if (available.Count == 0)
        {
            throw new NotSupportedException(
                "The target selection window does not expose a structurally decoded moSketchChain_c contour chain. " +
                "Only the measured explicit-contour SelectionAuthoring bases are supported. No stream changes have been made.");
        }

        var contours = ValidateRequestedContours(def, available);
        var suffix = SelectedSketchContourSuffixParser.Read(doc, chooser.TagOffset, sketchEndOffset);
        if (suffix == null || !suffix.IsValid)
        {
            throw new NotSupportedException(
                "The target document does not contain a complete native trim/reference suffix " +
                "(moSimpleSurfIdRep_c / moCStringHandle_c / trim edge records) for the selected sketch. " +
                "Envelope-only selection authoring is not native-valid and is refused. No stream changes have been made.");
        }
        if (string.IsNullOrEmpty(suffix.SourceDocumentPath))
        {
            throw new NotSupportedException(
                "The target moCStringHandle_c does not expose a readable source document path; the writer refuses " +
                "to emit the selection block. No stream changes have been made.");
        }

        ushort expectedSurface = contours.Count == 1
            ? SelectionExtrudeTemplates.SingleContourSurfaceId
            : SelectionExtrudeTemplates.TwoContourSurfaceId;
        if (suffix.SurfaceId != expectedSurface)
        {
            throw new NotSupportedException(
                $"The captured selection suffix surface 0x{suffix.SurfaceId:X2} does not match the requested " +
                $"{contours.Count}-contour layout (expected 0x{expectedSurface:X2}). No stream changes have been made.");
        }

        string expectedPrefixSha = contours.Count == 1
            ? SelectionExtrudeTemplates.OneContourPrefixSha256
            : SelectionExtrudeTemplates.TwoContourPrefixSha256;
        RequireControlledPrefix(currentStream, chooser.TagOffset, expectedPrefixSha, contours.Count);
        RequireStrippedFolderCount(currentStream);

        // Rebuild the selection window from the captured suffix and the requested chains.
        uint[][] contourIds = contours.Select(c => c.EntityIndices.ToArray()).ToArray();
        byte[] selectionBlock = SketchSelectionWriter.BuildContourSelectionBlock(
            contourIds, sketchId: 1, surfaceId: suffix.SurfaceId, suffix: suffix,
            targetDocumentPath: normalizedTarget);

        uint featureId = 0xC28F06D1u + (uint)ExtrudeFeatureBuilder.CountOccurrences(currentStream, ExtrudeClass);
        byte[] featureBlock = BuildFeatureBlock(def, featureName, contours.Count, featureId);

        byte[] newStream = new byte[chooser.TagOffset + selectionBlock.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, chooser.TagOffset);
        Array.Copy(selectionBlock, 0, newStream, chooser.TagOffset, selectionBlock.Length);
        Array.Copy(featureBlock, 0, newStream, chooser.TagOffset + selectionBlock.Length, featureBlock.Length);

        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        return newStream;
    }

    private static MfcElement FindChooser(MfcDocument doc, int sketchStart, int sketchEnd)
    {
        var choosers = doc.AllElements
            .Where(e => e.ClassName == ChooserClass && e.TagOffset >= sketchStart && e.TagOffset < sketchEnd)
            .OrderBy(e => e.TagOffset)
            .ToList();
        if (choosers.Count != 1)
        {
            throw new NotSupportedException(
                $"Explicit contour selection authoring requires exactly one complete {ChooserClass} window " +
                $"after the target sketch, but found {choosers.Count}. The target must be the extrusion-stripped " +
                "SelectionAuthoring base with a captured trim suffix. No stream changes have been made.");
        }
        return choosers[0];
    }

    private static void EnsureWindowIsLastData(MfcDocument doc, MfcElement chooser, int sketchEnd)
    {
        // The controlled base ends with the suffix; no feature objects may follow the window.
        var trailing = doc.AllElements
            .Where(e => e.TagOffset >= chooser.TagOffset && e.TagOffset < sketchEnd &&
                        (e.ClassName == ExtrudeClass || e.ClassName == CutExtrudeClass))
            .ToList();
        if (trailing.Count > 0)
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring requires an extrusion-stripped base; the target still " +
                "contains an extrusion after the selection window. No stream changes have been made.");
        }
    }

    private static void EnsureNoExistingExtrusion(MfcDocument doc, int windowStart)
    {
        if (doc.AllElements.Any(e => e.TagOffset >= windowStart &&
                                     (e.ClassName == ExtrudeClass || e.ClassName == CutExtrudeClass)))
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring currently supports only a base without any extrusion feature. " +
                "No stream changes have been made.");
        }
    }

    private static IReadOnlyList<SketchContourReference> ValidateRequestedContours(
        ExtrudeFeatureModel def, IReadOnlyList<SketchContourReference> available)
    {
        var requested = def.SelectedContours;
        if (requested == null || requested.Count < 1 || requested.Count > 2)
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring supports one or two contours. No stream changes have been made.");
        }
        if (requested.Count != available.Count)
        {
            throw new NotSupportedException(
                $"The target selection window exposes {available.Count} contour chains, but the definition " +
                $"requests {requested.Count}. The controlled base and the requested selection must match. " +
                "No stream changes have been made.");
        }

        var used = new bool[available.Count];
        for (int i = 0; i < requested.Count; i++)
        {
            var contour = requested[i];
            if (contour == null)
                throw new ArgumentException("SelectedContours must not contain null entries.", nameof(def));
            if (contour.IsCandidateOnly)
            {
                throw new NotSupportedException(
                    "Candidate-only contours are not accepted for authoring. No stream changes have been made.");
            }
            var ids = contour.EntityIndices;
            if (ids == null || ids.Count == 0)
                throw new ArgumentException("Each selected contour requires non-empty EntityIndices.", nameof(def));
            if (ids.Any(id => id == 0) || ids.Distinct().Count() != ids.Count)
                throw new ArgumentException("Contour entity ids must be non-zero and distinct.", nameof(def));

            // Only the measured chain sequences are supported; the feature template carries
            // contour-specific header/end-face/topology records for these exact loops.
            // The two-contour specimen also verifies the same loops in either group order,
            // so each requested sequence may match any not-yet-used available chain.
            int match = -1;
            for (int j = 0; j < available.Count; j++)
            {
                if (used[j]) continue;
                var availableIds = available[j].EntityIndices;
                if (availableIds != null && availableIds.SequenceEqual(ids))
                {
                    match = j;
                    break;
                }
            }
            if (match < 0)
            {
                string availableText = string.Join(" | ",
                    available.Select(c => "[" + string.Join(",", c.EntityIndices ?? Array.Empty<uint>()) + "]"));
                throw new NotSupportedException(
                    $"Selected contour {i + 1} [{string.Join(",", ids)}] does not match a measured contour chain " +
                    $"of the target base (available {availableText}). No stream changes have been made.");
            }
            used[match] = true;
        }

        return requested;
    }

    private static void RequireControlledPrefix(
        byte[] stream, int prefixEnd, string expectedSha256, int contourCount)
    {
        byte[] prefix = MfcPrimitives.CopyRange(stream, 0, prefixEnd);
        if (prefix.Length >= 6)
        {
            prefix[4] = 0;
            prefix[5] = 0;
        }
        string sha;
        using (var algorithm = SHA256.Create())
            sha = BitConverter.ToString(algorithm.ComputeHash(prefix)).Replace("-", string.Empty).ToLowerInvariant();
        if (!string.Equals(sha, expectedSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Explicit contour selection authoring is limited to the controlled SelectionAuthoring " +
                $"{contourCount}-contour dual-rectangle sketch (prefix signature {sha.Substring(0, 12)} does not " +
                "match the measured base). No stream changes have been made.");
        }
    }

    private static void RequireStrippedFolderCount(byte[] stream)
    {
        ushort folderCount = BitConverter.ToUInt16(stream, 4);
        if (folderCount != SelectionExtrudeTemplates.ExpectedStrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Explicit contour selection authoring requires the extrusion-stripped base with folder count " +
                $"{SelectionExtrudeTemplates.ExpectedStrippedFolderCount}, but the document uses {folderCount}. " +
                "No stream changes have been made.");
        }
    }

    private static byte[] BuildFeatureBlock(
        ExtrudeFeatureModel def, string featureName, int contourCount, uint featureId)
    {
        byte[] headerRest = (contourCount == 1
            ? SelectionExtrudeTemplates.OneContourHeaderRest
            : SelectionExtrudeTemplates.TwoContourHeaderRest).ToArray();
        int featureIdOffset = contourCount == 1
            ? SelectionExtrudeTemplates.OneContourFeatureIdOffset
            : SelectionExtrudeTemplates.TwoContourFeatureIdOffset;
        BitConverter.GetBytes(featureId).CopyTo(headerRest, featureIdOffset);
        byte[] tail = contourCount == 1
            ? SelectionExtrudeTemplates.OneContourTail
            : SelectionExtrudeTemplates.TwoContourTail;

        using var writer = new MfcStreamWriter(4096);
        writer.WriteMfcTag(ExtrudeClass);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteBytes(headerRest);
        writer.WriteBytes(tail);
        return writer.ToArray();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeAdditionalOptionParser.cs
// ------------------------------------------------------------------------
internal static class ExtrudeAdditionalOptionParser
{
    internal static void Apply(MfcDocument doc, string className, int start, int end, ExtrudeFeatureModel definition)
    {
        var ctx = FeatureWindowContext.TryCreate(
            doc,
            className,
            0,
            start,
            end,
            expectedClassName: "moExtrusion_c",
            expectedStreamVersion: 0x70);
        if (ctx == null) return;

        var specNode = ctx.GetSingleNode("moEndSpec_c");
        if (specNode == null) return;
        var spec = specNode.RawData;

        ReadTwoDirections(ctx, spec, definition);
        if (spec.Length < 24 || BitConverter.ToUInt16(spec, 0) != 0 ||
            BitConverter.ToUInt32(spec, 2) != 1 ||
            BitConverter.ToUInt32(spec, 6) != 0 ||
            BitConverter.ToUInt32(spec, 10) > 1 ||
            BitConverter.ToUInt16(spec, 14) != 0) return;

        ReadDraft(ctx, definition);
        var from = ctx.GetSingleNode("moFromEndSpec_c");
        if (from != null)
        {
            var offset = ReadStartOffset(MfcPrimitives.CopyRange(ctx.RawData, from.DataStart, ctx.End));
            if (offset.HasValue)
            {
                definition.FromType = 3;
                definition.FromOffsetDistance = offset.Value;
                definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.FromType));
                definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.FromOffsetDistance));
            }
        }
    }

    private static void ReadTwoDirections(FeatureWindowContext ctx, byte[] spec, ExtrudeFeatureModel definition)
    {
        if (spec.Length < 24 || BitConverter.ToUInt16(spec, 0) != 0 ||
            BitConverter.ToUInt32(spec, 2) != 0 || BitConverter.ToUInt32(spec, 6) != 0 ||
            BitConverter.ToUInt32(spec, 10) > 1 || BitConverter.ToUInt16(spec, 14) != 0 ||
            BitConverter.ToUInt32(spec, 16) != 0) return;

        var favorite = ctx.GetSingleNode("moFavoriteHandle_c");
        var from = ctx.GetSingleNode("moFromEndSpec_c");
        if (favorite == null || from == null) return;

        var source = ctx.RawData;
        int secondSpec = favorite.DataStart + 436;
        int bound = from.TagOffset;
        if (secondSpec + 18 > bound || bound > ctx.End || BitConverter.ToUInt16(source, secondSpec) != 0x810B) return;
        for (int i = secondSpec + 2; i < secondSpec + 18; i++) if (source[i] != 0) return;
        int parameter = favorite.DataStart + 1111;
        if (parameter + 10 > bound || parameter - 8 < secondSpec) return;
        if (BitConverter.ToUInt16(source, parameter - 8) != 0x810F ||
            BitConverter.ToUInt32(source, parameter - 6) != 2 ||
            BitConverter.ToUInt16(source, parameter - 2) != 0x8111) return;

        var raw = MfcPrimitives.CopyRange(source, parameter, bound);
        var depth = MfcParameterReader.ReadNamedDouble(raw, 0, "D2", requireDNumberFormat: true);
        if (!depth.HasValue || depth <= 0) return;
        definition.BothDirections = true;
        definition.DepthDirection2 = depth.Value;
        definition.EndConditionDirection2 = 0;
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.BothDirections));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DepthDirection2));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.EndConditionDirection2));
    }

    private static void ReadDraft(FeatureWindowContext ctx, ExtrudeFeatureModel definition)
    {
        var angleNode = ctx.GetSingleNode("moAngleParameter_c");
        if (angleNode == null) return;

        var nodes = ctx.Nodes;
        int index = -1;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], angleNode)) { index = i; break; }
        }
        if (index < 3 || nodes[index - 3].ClassName != "moFavoriteHandle_c" ||
            nodes[index - 1].ClassName != "AngleDim_c" ||
            nodes[index - 2].ClassName != "moDisplayAngularDim_c") return;

        var display = nodes[index - 2];
        var source = ctx.RawData;
        int flag = display.TagOffset - 24;
        if (flag < nodes[index - 3].DataStart || BitConverter.ToUInt32(source, flag) != 1) return;
        for (int i = flag + 4; i < display.TagOffset; i++)
            if (source[i] != 0) return;

        byte[]? raw = ctx.SlicePayload(angleNode);
        if (raw == null) return;
        var value = ReadNamedParameter(raw, 0);
        if (!value.HasValue || value.Value <= 0 || value.Value >= Math.PI / 2) return;
        definition.DraftWhileExtruding = true;
        definition.DraftAngle = value.Value;
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DraftWhileExtruding));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DraftAngle));
    }

    internal static double? ReadStartOffset(byte[] raw)
    {
        if (raw.Length < 24 || BitConverter.ToUInt32(raw, 0) != 5 ||
            (BitConverter.ToUInt16(raw, 4) & 0x8000) == 0) return null;
        for (int i = 6; i < 22; i++) if (raw[i] != 0) return null;
        double? result = null;
        int count = 0;
        for (int i = 22; i + 6 < raw.Length; i++)
        {
            if (raw[i] != 4 || raw[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(raw, i + 2)) continue;
            var value = ReadNamedParameter(raw, i);
            if (!value.HasValue) continue;
            count++;
            result = value;
        }
        return count == 1 && result >= 0 && !double.IsInfinity(result.Value) ? result : null;
    }

    internal static double? ReadNamedParameter(byte[] raw, int offset) =>
        MfcParameterReader.ReadNamedDouble(raw, offset, expectedName: null, requireDNumberFormat: true);

    internal static List<(int Offset, double Value)> FindNamedParameters(byte[] raw, int start, int end, string name) =>
        MfcParameterReader.FindNamedDoubles(raw, start, end, name, requireDNumberFormat: true);
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeCutAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured 0x71 moICE_c feature block and stripped-base signature. The block starts at the class
/// tag and runs to the end of the native stream; the base is truncated there with folder count and
/// the preceding 0x67 value zeroed for signature normalization.
/// </summary>
internal sealed class ExtrudeCutAuthoringTemplate
{
    internal ExtrudeCutAuthoringTemplate(string name, string baseSha256, int baseLength,
        ushort streamVersion, int strippedFolderCount, int markerValue, byte[] block)
    {
        Name = name;
        BaseSha256 = baseSha256;
        BaseLength = baseLength;
        StreamVersion = streamVersion;
        StrippedFolderCount = strippedFolderCount;
        MarkerValue = markerValue;
        Block = block;
    }

    internal string Name { get; }
    internal string BaseSha256 { get; }
    internal int BaseLength { get; }
    internal ushort StreamVersion { get; }
    internal int StrippedFolderCount { get; }
    internal int MarkerValue { get; }
    internal byte[] Block { get; }
}

internal static class ExtrudeCutAuthoringTemplates
{
    /// <summary>direction-2 draft cut with face context measured template from extrude_draft_dir2_3deg.resolved.bin.</summary>
    internal static readonly ExtrudeCutAuthoringTemplate DraftDirection2 = new ExtrudeCutAuthoringTemplate(
        "DraftDir2",
        "43aac99ad328b9f2c85e8701ea8aff0a2664a696584a0ae3bf1bd9a284b12181",
        17982,
        0x71,
        21,
        3,
        ExtrudeAuthoringSegments.CutMergeFalseSource);

    /// <summary>Merge=false multi-body ICE feature measured template from extrude_merge_false.resolved.bin.</summary>
    internal static readonly ExtrudeCutAuthoringTemplate MergeFalse = new ExtrudeCutAuthoringTemplate(
        "MergeFalseBoss",
        "1c25a7d33c814eb383756443dd5563ca82608a3a48960b1a6b439293f8e51d8e",
        15181,
        0x71,
        21,
        3,
        ExtrudeAuthoringSegments.CutMergeFalseResave);

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeCutFlagAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured 0x71 flip-side/normal cut tail and stripped-base signature. The block starts at the
/// moICE_c tag and runs to the end of the native stream (NormalCut also carries its following
/// sheet-metal/template folders); the base is truncated at that tag with folder count and the
/// preceding 0x67 value zeroed for signature normalization.
/// </summary>
internal sealed class ExtrudeCutFlagAuthoringTemplate
{
    internal ExtrudeCutFlagAuthoringTemplate(string kind, string name, string baseSha256,
        int baseLength, ushort streamVersion, int nativeFolderCount, int strippedFolderCount,
        int markerValue, byte[] block)
    {
        Kind = kind;
        Name = name;
        BaseSha256 = baseSha256;
        BaseLength = baseLength;
        StreamVersion = streamVersion;
        NativeFolderCount = nativeFolderCount;
        StrippedFolderCount = strippedFolderCount;
        MarkerValue = markerValue;
        Block = block;
    }

    internal string Kind { get; }
    internal string Name { get; }
    internal string BaseSha256 { get; }
    internal int BaseLength { get; }
    internal ushort StreamVersion { get; }
    internal int NativeFolderCount { get; }
    internal int StrippedFolderCount { get; }
    internal int MarkerValue { get; }
    internal byte[] Block { get; }
}

internal static class ExtrudeCutFlagAuthoringTemplates
{
    /// <summary>flip-side source measured template from extrude_flip_side_cut.resolved.bin.</summary>
    internal static readonly ExtrudeCutFlagAuthoringTemplate FlipSource = new ExtrudeCutFlagAuthoringTemplate(
        "flip",
        "FlipSideCut",
        "ba457308668cd951238ec43dbc4f565079878930d0c4f66944ac960fa0a607de",
        17983,
        0x71,
        22,
        21,
        11,
        ExtrudeAuthoringSegments.CutFlagNormalCut);

    /// <summary>flip-side resave measured template from extrude_flip_side_cut_resave.resolved.bin.</summary>
    internal static readonly ExtrudeCutFlagAuthoringTemplate FlipResave = new ExtrudeCutFlagAuthoringTemplate(
        "flip",
        "FlipSideCut",
        "ca78b8f6e323cc5bd428a508574cc5df42e4dab885ba3b73a790bb83346ae384",
        18369,
        0x71,
        22,
        21,
        11,
        ExtrudeAuthoringSegments.CutFlagFlipSideToCut);

    /// <summary>normal-cut source measured template from extrude_normal_cut.resolved.bin.</summary>
    internal static readonly ExtrudeCutFlagAuthoringTemplate NormalSource = new ExtrudeCutFlagAuthoringTemplate(
        "normal",
        "NormalCut",
        "878d6f504af9e751b30ebc73476f0180f16b53481a928ddaeb6a815ae498c4d1",
        24026,
        0x74,
        24,
        19,
        11,
        ExtrudeAuthoringSegments.CutFlagOptimizeGeometry);

    /// <summary>normal-cut resave measured template from extrude_normal_cut_resave.resolved.bin.</summary>
    internal static readonly ExtrudeCutFlagAuthoringTemplate NormalResave = new ExtrudeCutFlagAuthoringTemplate(
        "normal",
        "NormalCut",
        "29359a2936f7c438d6f90381ec059cf1acce938f6a5fcee653d996dab5e204d2",
        24408,
        0x74,
        24,
        19,
        11,
        ExtrudeAuthoringSegments.CutFlagNormalCutResave);

};

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeCutFlagFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured template authoring for the 0x71 flip-side and normal cut layouts. The full native
/// tail is appended to its exact stripped base; the flip-side/normal-cut state is carried by the
/// measured block rather than a re-synthesized scalar. No geometry/option parameter is varied.
/// </summary>
internal static class ExtrudeCutFlagFeatureBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static bool IsFlipTemplate(ExtrudeFeatureModel definition) =>
        definition.IsCut && !definition.IsThinFeature && definition.FlipSideToCut && !definition.NormalCut;

    internal static bool IsNormalTemplate(ExtrudeFeatureModel definition) =>
        definition.IsCut && !definition.IsThinFeature && definition.NormalCut && !definition.FlipSideToCut;

    internal static bool IsTemplate(ExtrudeFeatureModel definition) =>
        definition.IsCut && !definition.IsThinFeature &&
        (definition.FlipSideToCut || definition.NormalCut);

    internal static string GetTemplateName(ExtrudeFeatureModel definition) =>
        IsNormalTemplate(definition)
            ? ExtrudeCutFlagAuthoringTemplates.NormalSource.Name
            : ExtrudeCutFlagAuthoringTemplates.FlipSource.Name;

    internal static void ValidateDefinition(ExtrudeFeatureModel definition)
    {
        bool flip = IsFlipTemplate(definition);
        bool normal = IsNormalTemplate(definition);
        if (!flip && !normal)
        {
            throw new NotSupportedException(
                "Flip-side/normal cut authoring requires exactly one of FlipSideToCut or NormalCut. " +
                "No stream changes have been made.");
        }
        Reject(definition.IsThinFeature, nameof(definition.IsThinFeature));
        Reject(!definition.IsCut, nameof(definition.IsCut));
        Reject(definition.EndCondition != 1, nameof(definition.EndCondition));
        Reject(definition.EndConditionDirection2 != 0, nameof(definition.EndConditionDirection2));
        Reject(definition.Depth != 0, nameof(definition.Depth));
        Reject(definition.DepthDirection2 != 0, nameof(definition.DepthDirection2));
        Reject(definition.BothDirections, nameof(definition.BothDirections));
        Reject(definition.ReverseDirection, nameof(definition.ReverseDirection));
        Reject(!definition.Merge, nameof(definition.Merge));
        Reject(definition.DraftWhileExtruding, nameof(definition.DraftWhileExtruding));
        Reject(definition.DraftAngle != 0, nameof(definition.DraftAngle));
        Reject(definition.DraftWhileExtrudingDirection2, nameof(definition.DraftWhileExtrudingDirection2));
        Reject(definition.DraftAngleDirection2 != 0, nameof(definition.DraftAngleDirection2));
        Reject(definition.DraftOutward, nameof(definition.DraftOutward));
        Reject(definition.DraftOutwardDirection2, nameof(definition.DraftOutwardDirection2));
        Reject(definition.FromType != 0, nameof(definition.FromType));
        Reject(definition.FromOffsetDistance != 0, nameof(definition.FromOffsetDistance));
        Reject(definition.FromOffsetReverse, nameof(definition.FromOffsetReverse));
        Reject(definition.AssemblyFeatureScope, nameof(definition.AssemblyFeatureScope));
        Reject(definition.AutoSelectComponents, nameof(definition.AutoSelectComponents));
        Reject(definition.PropagateFeatureToParts, nameof(definition.PropagateFeatureToParts));
        Reject(definition.OptimizeGeometry, nameof(definition.OptimizeGeometry));
        Reject(definition.ThinWallType != 0, nameof(definition.ThinWallType));
        Reject(definition.WallThickness != 0, nameof(definition.WallThickness));
        Reject(definition.WallThicknessDirection2 != 0, nameof(definition.WallThicknessDirection2));
        Reject(definition.CapEnds, nameof(definition.CapEnds));
        Reject(definition.CapThickness != 0, nameof(definition.CapThickness));
        Reject(definition.LinkToThickness, nameof(definition.LinkToThickness));
        Reject(definition.TranslateSurface, nameof(definition.TranslateSurface));
        Reject(definition.TranslateSurfaceDirection2, nameof(definition.TranslateSurfaceDirection2));
        Reject(definition.ReverseOffset, nameof(definition.ReverseOffset));
        Reject(definition.ReverseOffsetDirection2, nameof(definition.ReverseOffsetDirection2));
        Reject(definition.AutoFilletCorners, nameof(definition.AutoFilletCorners));
        Reject(definition.AutoFilletRadius != 0, nameof(definition.AutoFilletRadius));
        Reject(definition.SketchSelectionMode == SketchSelectionMode.ExplicitSelection, nameof(definition.SketchSelectionMode));
        Reject(definition.SelectedContours != null && definition.SelectedContours.Count > 0, nameof(definition.SelectedContours));
        Reject(definition.SelectedRegions != null && definition.SelectedRegions.Count > 0, nameof(definition.SelectedRegions));
        Reject(definition.DirectionReference != null, nameof(definition.DirectionReference));
        Reject(definition.EndConditionEntity != null, nameof(definition.EndConditionEntity));
        Reject(definition.BodyScope != null && (!definition.BodyScope.AutoSelect || definition.BodyScope.Flags != 2 ||
            (definition.BodyScope.BodyIds != null && definition.BodyScope.BodyIds.Count > 0) ||
            (definition.BodyScope.Bodies != null && definition.BodyScope.Bodies.Count > 0)), nameof(definition.BodyScope));
    }

    internal static byte[] Build(
        byte[] currentStream, ExtrudeFeatureModel definition, string featureName)
    {
        ValidateDefinition(definition);
        var template = SelectByBaseLength(currentStream, IsNormalTemplate(definition));
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cut template authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (currentStream.Length != template.BaseLength || BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                "Flip-side/normal cut authoring requires the exact measured stripped base and folder count. " +
                "No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 0) != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Flip-side/normal cut authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}. " +
                "No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "Flip-side/normal cut authoring requires the stripped base's 0x67 marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Flip-side/normal cut authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)template.NativeFolderCount).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static ExtrudeCutFlagAuthoringTemplate SelectByBaseLength(byte[] currentStream, bool normal)
    {
        int length = currentStream?.Length ?? 0;
        var source = normal
            ? ExtrudeCutFlagAuthoringTemplates.NormalSource
            : ExtrudeCutFlagAuthoringTemplates.FlipSource;
        var resave = normal
            ? ExtrudeCutFlagAuthoringTemplates.NormalResave
            : ExtrudeCutFlagAuthoringTemplates.FlipResave;
        if (length == source.BaseLength) return source;
        if (length == resave.BaseLength) return resave;
        throw new NotSupportedException(
            $"Flip-side/normal cut authoring requires the measured stripped base length " +
            $"{source.BaseLength} or {resave.BaseLength}; the document stream is {length} bytes. " +
            "No stream changes have been made.");
    }

    private static void Reject(bool condition, string property)
    {
        if (condition)
            throw new NotSupportedException(
                $"Flip-side/normal cut authoring does not support {property}. No stream changes have been made.");
    }

    private static string NormalizedBaseSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        int marker = stream.Length - MarkerDistanceFromFeature;
        BitConverter.GetBytes(0u).CopyTo(normalized, marker + 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeCutOptionParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict reader for the extrusion options measured on ResolvedFeatures version-0x71 cut samples:
/// direction-1 draft, direction-2 draft (with direction-2 depth), and the isolated no-merge layout.
/// </summary>
internal static class ExtrudeCutOptionParser
{
    internal static void Apply(
        MfcDocument doc,
        ushort streamVersion,
        string className,
        int start,
        int end,
        ExtrudeFeatureModel definition)
    {
        if (className != "moICE_c" || definition == null) return;
        byte[] raw = doc.SourceData;
        if (streamVersion != 0x71)
        {
            if (streamVersion == 0x74) ReadMeasuredCutFlag(raw, start, definition);
            return;
        }

        var ctx = FeatureWindowContext.TryCreate(doc, className, 0, start, end, expectedStreamVersion: 0x71);
        if (ctx == null) return;

        var d1 = ctx.Parameters.FindAll("D1");
        var d2 = ctx.Parameters.FindAll("D2");
        if (d1.Count == 1 && IsPositiveFinite(d1[0].Value))
        {
            definition.Depth = d1[0].Value;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.Depth));
        }

        var displays = ctx.GetNodes("moDisplayAngularDim_c");
        var angleDims = ctx.GetNodes("AngleDim_c");
        var angleParameters = ctx.GetNodes("moAngleParameter_c");
        if (displays.Count > 0 || angleDims.Count > 0 || angleParameters.Count > 0)
        {
            ReadDraft(raw, ctx.Start, ctx.End, displays, angleDims, angleParameters, d2, definition);
        }

        if (TryReadMergeFlag(ctx, out bool merge))
        {
            definition.Merge = merge;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.Merge));
        }

        ReadMeasuredCutFlag(raw, start, definition);
        TryReadEmbeddedOffsetReverse(raw, ctx.Start, ctx.End, definition);
    }

    private static void TryReadEmbeddedOffsetReverse(
        byte[] raw, int start, int limit, ExtrudeFeatureModel definition)
    {
        int candidate = -1;
        for (int i = start; i + 22 <= limit; i++)
        {
            if (BitConverter.ToUInt32(raw, i) != 5) continue;
            if ((BitConverter.ToUInt16(raw, i + 4) & 0x8000) == 0) continue;
            bool zeros = true;
            for (int j = i + 6; j < i + 22; j++) if (raw[j] != 0) { zeros = false; break; }
            if (!zeros) continue;
            if (candidate >= 0) return;
            candidate = i;
        }
        if (candidate < 0) return;
        var slice = MfcPrimitives.CopyRange(raw, candidate, limit);
        if (slice == null) return;
        double? offset = ExtrudeAdditionalOptionParser.ReadStartOffset(slice);
        if (!offset.HasValue || offset.Value <= 0 || double.IsInfinity(offset.Value)) return;
        definition.FromType = 3;
        definition.FromOffsetDistance = offset.Value;
        definition.FromOffsetReverse = true;
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.FromType));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.FromOffsetDistance));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.FromOffsetReverse));
    }

    private static void ReadMeasuredCutFlag(byte[] raw, int start, ExtrudeFeatureModel definition)
    {
        int length = raw.Length - start;
        if (length <= 0) return;
        if (TailMatches(raw, start, ExtrudeCutFlagAuthoringTemplates.FlipSource.Block) ||
            TailMatches(raw, start, ExtrudeCutFlagAuthoringTemplates.FlipResave.Block))
        {
            definition.FlipSideToCut = true;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.FlipSideToCut));
            return;
        }
        if (TailMatches(raw, start, ExtrudeCutFlagAuthoringTemplates.NormalSource.Block) ||
            TailMatches(raw, start, ExtrudeCutFlagAuthoringTemplates.NormalResave.Block))
        {
            definition.NormalCut = true;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.NormalCut));
        }
    }

    private static bool TailMatches(byte[] raw, int start, byte[] block)
    {
        if (raw.Length - start != block.Length) return false;
        for (int i = 0; i < block.Length; i++)
            if (raw[start + i] != block[i]) return false;
        return true;
    }

    private static bool TryReadMergeFlag(FeatureWindowContext ctx, out bool merge)
    {
        merge = false;
        var endFace = ctx.GetSingleNode("moEndFace3IntSurfIdRep_c");
        if (endFace == null) return false;
        byte[] raw = ctx.RawData;
        int data = endFace.DataStart;
        if (data < 0 || data + 18 > ctx.End) return false;
        if (raw[data] != 0 || raw[data + 1] != 0 ||
            raw[data + 2] != 0xEE || raw[data + 3] != 0x80 ||
            raw[data + 4] != 0xF1 || raw[data + 5] != 0x00 ||
            BitConverter.ToUInt32(raw, data + 6) != 0x56) return false;
        uint flag = BitConverter.ToUInt32(raw, data + 14);
        if (flag > 1) return false;
        merge = flag == 1;
        return true;
    }

    private static void ReadDraft(
        byte[] raw,
        int start,
        int limit,
        List<MfcElement> displays,
        List<MfcElement> angleDims,
        List<MfcElement> angleParameters,
        List<(int Offset, double Value)> d2,
        ExtrudeFeatureModel definition)
    {
        if (displays.Count != 1 || angleDims.Count != 1 || angleParameters.Count != 1) return;
        var display = displays[0];
        var angleDim = angleDims[0];
        var parameter = angleParameters[0];
        if (!(display.TagOffset < angleDim.TagOffset && angleDim.TagOffset < parameter.TagOffset)) return;

        bool forward = TryReadForwardFlag(raw, display.TagOffset, start, limit, out bool outward);
        bool reverse = FlagWithZeros(raw, display.TagOffset - 22, 18, start, limit);
        if (forward == reverse) return;

        var angle = MfcParameterReader.ReadNamedDouble(raw, parameter.DataStart, requireDNumberFormat: true);
        if (!angle.HasValue || !IsPositiveFinite(angle.Value) || angle.Value >= Math.PI / 2.0) return;
        int namePosition = parameter.DataStart + 2;
        string? angleName = MfcPrimitives.ReadWChar(raw, ref namePosition);

        if (forward)
        {
            if (angleName != "D3" || d2.Count != 0) return;
            definition.DraftWhileExtruding = true;
            definition.DraftAngle = angle.Value;
            if (outward)
            {
                definition.DraftOutward = true;
                definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DraftOutward));
            }
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DraftWhileExtruding));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DraftAngle));
        }
        else
        {
            if (angleName != "D4" || d2.Count != 1 || !IsPositiveFinite(d2[0].Value)) return;
            definition.BothDirections = true;
            definition.DepthDirection2 = d2[0].Value;
            definition.EndConditionDirection2 = 0;
            definition.DraftWhileExtrudingDirection2 = true;
            definition.DraftAngleDirection2 = angle.Value;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.BothDirections));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DepthDirection2));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.EndConditionDirection2));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DraftWhileExtrudingDirection2));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DraftAngleDirection2));
        }
    }

    private static bool TryReadForwardFlag(
        byte[] raw, int displayTag, int start, int limit, out bool outward)
    {
        outward = false;
        int flag = displayTag - 24;
        if (flag < start || flag + 24 > limit) return false;
        if (BitConverter.ToUInt32(raw, flag) != 1) return false;
        for (int i = flag + 4; i < flag + 8; i++) if (raw[i] != 0) return false;
        uint outwardValue = BitConverter.ToUInt32(raw, flag + 8);
        if (outwardValue > 1) return false;
        for (int i = flag + 12; i < flag + 20; i++) if (raw[i] != 0) return false;
        outward = outwardValue == 1;
        return true;
    }

    private static bool FlagWithZeros(byte[] raw, int offset, int zeroCount, int start, int limit)
    {
        if (offset < start || offset + 4 + zeroCount > limit) return false;
        if (BitConverter.ToUInt32(raw, offset) != 1) return false;
        for (int i = offset + 4; i < offset + 4 + zeroCount; i++)
            if (raw[i] != 0) return false;
        return true;
    }

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeCutTemplateBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Controlled template authoring for the measured 0x71 cut layouts:
/// - direction-2 draft cut on the extrude_draft_dir2_3deg base (D4 angle patched);
/// - Merge=false multi-body ICE feature on the extrude_merge_false base (measured geometry fixed).
/// Complete native blocks are appended to their exact stripped bases; other layouts are rejected.
/// </summary>
internal static class ExtrudeCutTemplateBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static bool IsDraftDirection2Template(ExtrudeFeatureModel definition) =>
        definition.IsCut && !definition.IsThinFeature && definition.Merge &&
        (definition.BothDirections || definition.DraftWhileExtrudingDirection2 ||
         definition.DraftAngleDirection2 != 0 || definition.DraftOutwardDirection2);

    internal static bool IsMergeFalseTemplate(ExtrudeFeatureModel definition) =>
        definition.IsCut && !definition.IsThinFeature && !definition.Merge;

    internal static string GetTemplateName(ExtrudeFeatureModel definition) =>
        Select(definition).Name;

    private static ExtrudeCutAuthoringTemplate Select(ExtrudeFeatureModel definition)
    {
        if (IsMergeFalseTemplate(definition))
            return ExtrudeCutAuthoringTemplates.MergeFalse;
        if (IsDraftDirection2Template(definition))
            return ExtrudeCutAuthoringTemplates.DraftDirection2;
        throw new NotSupportedException("This cut layout has no template authoring block.");
    }

    internal static byte[] Build(
        byte[] currentStream, ExtrudeFeatureModel definition, string featureName)
    {
        var template = Select(definition);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cut template authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (currentStream == null || currentStream.Length != template.BaseLength)
        {
            throw new NotSupportedException(
                $"Cut template authoring requires the measured stripped base of length " +
                $"{template.BaseLength} bytes; the document stream is {currentStream?.Length ?? 0} bytes. " +
                "No stream changes have been made.");
        }
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Cut template authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Cut template authoring requires the stripped folder count {template.StrippedFolderCount}. " +
                "No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "Cut template authoring requires the stripped base's 0x67 marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream, marker);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cut template authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        if (IsDraftDirection2Template(definition))
            PatchDraftDirection2(block, definition.DraftAngleDirection2);

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static void PatchDraftDirection2(byte[] block, double angle)
    {
        var d4 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D4");
        if (d4.Count != 1)
            throw new InvalidOperationException("Measured direction-2 draft template is missing D4.");
        foreach (int off in new[] { 0x20, 0x23A })
        {
            if (Math.Abs(BitConverter.ToDouble(block, d4[0].Offset + off) - 3.0 * Math.PI / 180.0) > 1e-12)
                throw new InvalidOperationException(
                    "Measured direction-2 draft template layout changed; refusing to patch an unknown slot.");
            BitConverter.GetBytes(angle).CopyTo(block, d4[0].Offset + off);
        }
    }

    private static string NormalizedBaseSha(byte[] stream, int marker)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        BitConverter.GetBytes(0u).CopyTo(normalized, marker + 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Parametric serializer and injector for SolidWorks extrusion features (moExtrusion_c and moICE_c).
/// Generates compliant MFC 4.2 CArchive binary structures for insertion into ResolvedFeatures streams.
/// </summary>
internal static class ExtrudeFeatureBuilder
{
    /// <summary>
    /// Builds a new ResolvedFeatures stream containing the injected extrusion feature.
    /// </summary>
    public static byte[] BuildExtrusionStream(
        byte[] currentStream,
        ExtrudeFeatureModel def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName,
        string? targetDocumentPath = null,
        SketchFeatureModel? targetSketch = null)
    {
        ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        EnsureSupportedStreamVersion(currentStream, def);

        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));

        // Measured thin class authoring uses the complete native moExtruThin_c block on its own
        // stripped base (OneDirection / CapEnds templates). It never goes through the solid
        // moExtrusion_c serializer.
        if (def.IsThinFeature)
        {
            return ExtrudeThinFeatureBuilder.Build(
                currentStream, def, targetSketchFolder, sketchEndOffset, featureName);
        }
        if (ExtrudeCutTemplateBuilder.IsDraftDirection2Template(def) ||
            ExtrudeCutTemplateBuilder.IsMergeFalseTemplate(def))
        {
            return ExtrudeCutTemplateBuilder.Build(currentStream, def, featureName);
        }
        if (ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(def))
        {
            return ExtrudeUpToSurfaceTemplateBuilder.Build(currentStream, def, featureName);
        }
        if (ExtrudeCutFlagFeatureBuilder.IsTemplate(def))
        {
            return ExtrudeCutFlagFeatureBuilder.Build(currentStream, def, featureName);
        }

        // Explicit contour/region selection is only emitted on the measured SelectionAuthoring
        // bases with captured native context. The selection window is replaced in place and
        // the measured selection-specific feature block is appended.
        if (def.SketchSelectionMode == SketchSelectionMode.ExplicitSelection)
        {
            if (def.SelectedRegions != null && def.SelectedRegions.Count > 0)
            {
                return RegionSelectionExtrudeBuilder.Build(
                    currentStream, def, targetSketchFolder, sketchEndOffset, featureName,
                    targetDocumentPath, targetSketch);
            }
            return ExplicitSelectionExtrudeBuilder.Build(
                currentStream, def, targetSketchFolder, sketchEndOffset, featureName, targetDocumentPath);
        }

        // 1. Require a structurally decoded sketch chain and a real sketch child-count marker.
        // Synthesis on sketches without a chain (e.g. unconsumed sketches in version 0x6E/0x70
        // parts) produced native-unloadable files, so this path fails fast before any mutation.
        var entityIds = MfcExtrudeSerializer.ExtractEntityIds(targetSketchFolder, currentStream, sketchEndOffset);
        int markerOffset = FindSketchChildCountMarker(currentStream, targetSketchFolder, sketchEndOffset);

        // 2. Determine unique surface tag and feature id based on existing extrusion counts
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        int existingBossCount = CountOccurrences(currentStream, "moExtrusion_c");
        int existingCutCount = CountOccurrences(currentStream, "moICE_c");

        uint surfaceTag = 0x6AE19B71 + ((uint)existingBossCount * 0x10);
        uint cutSurfaceTag = 0x6AE19B83 + ((uint)existingCutCount * 0x10);
        uint baseSurfaceTag = 0x6AE19B82;
        uint uniqueFeatureId = def.IsCut
            ? 0xCD0C81A9 + (uint)existingCutCount
            : 0xC28F06D1 + (uint)existingBossCount;

        // 3. Synthesize feature block via modular serializer
        byte[] featureBlock = def.IsCut
            ? MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds, cutSurfaceTag, baseSurfaceTag, uniqueFeatureId)
            : MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds, surfaceTag, uniqueFeatureId);

        // 4. Allocate new stream buffer
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);

        // 5. Increment stream header folder count at offset 4
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);

        // 6. Update target sketch child count flag (tag 0x67); the marker was validated above.
        uint currentChildCount = BitConverter.ToUInt32(currentStream, markerOffset + 4);
        BitConverter.GetBytes(currentChildCount + 1).CopyTo(newStream, markerOffset + 4);

        // 7. Append synthesized feature block
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    /// <summary>
    /// Synthesizes a complete Boss Extrude (moExtrusion_c) binary block with parametric dimensions.
    /// </summary>
    internal static byte[] BuildBossExtrudeBlock(
        ExtrudeFeatureModel def,
        string featureName = "凸台-拉伸1",
        IReadOnlyList<uint>? entityIds = null,
        uint surfaceTag = 0x6AE19B71,
        uint uniqueFeatureId = 0xC28F06D1)
    {
        ValidateDefinition(def);
        entityIds ??= new uint[] { 1, 2, 3, 4 };
        return MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds, surfaceTag, uniqueFeatureId);
    }

    /// <summary>
    /// Overload accepting entity count.
    /// </summary>
    internal static byte[] BuildBossExtrudeBlock(ExtrudeFeatureModel def, string featureName, int entityCount)
    {
        ValidateDefinition(def);
        var entityIds = (entityCount > 0)
            ? Enumerable.Range(1, entityCount).Select(i => (uint)i).ToList()
            : new List<uint> { 1, 2, 3, 4 };
        return MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds);
    }

    /// <summary>
    /// Synthesizes a complete Cut Extrude (moICE_c) binary block with parametric dimensions.
    /// </summary>
    internal static byte[] BuildCutExtrudeBlock(
        ExtrudeFeatureModel def,
        string featureName = "切除-拉伸1",
        IReadOnlyList<uint>? entityIds = null,
        uint cutSurfaceTag = 0x6AE19B83,
        uint baseSurfaceTag = 0x6AE19B82,
        uint uniqueFeatureId = 0xCD0C81A9)
    {
        ValidateDefinition(def);
        entityIds ??= new uint[] { 5 };
        return MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds, cutSurfaceTag, baseSurfaceTag, uniqueFeatureId);
    }

    /// <summary>
    /// Overload accepting entity count.
    /// </summary>
    internal static byte[] BuildCutExtrudeBlock(ExtrudeFeatureModel def, string featureName, int entityCount)
    {
        ValidateDefinition(def);
        var entityIds = (entityCount == 1)
            ? new List<uint> { 5 }
            : (entityCount > 0)
                ? Enumerable.Range(1, entityCount).Select(i => (uint)i).ToList()
                : new List<uint> { 5 };
        return MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds);
    }

    internal static void ValidateDefinition(ExtrudeFeatureModel def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (ExtrudeCutFlagFeatureBuilder.IsTemplate(def))
        {
            ExtrudeCutFlagFeatureBuilder.ValidateDefinition(def);
            return;
        }

        if (ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(def))
        {
            ValidateUpToSurfaceTemplate(def);
            return;
        }

        if (double.IsNaN(def.Depth) || double.IsInfinity(def.Depth) || def.Depth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Depth),
                $"Extrusion depth must be a positive finite number, but received {def.Depth}.");
        }

        if (def.BothDirections && (double.IsNaN(def.DepthDirection2) || double.IsInfinity(def.DepthDirection2) || def.DepthDirection2 <= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(def.DepthDirection2),
                $"Direction 2 extrusion depth must be a positive finite number, but received {def.DepthDirection2}.");
        }

        if (def.IsThinFeature)
        {
            ValidateThinDefinition(def);
            return;
        }
        if (ExtrudeCutTemplateBuilder.IsDraftDirection2Template(def))
        {
            ValidateDraftDirection2Template(def);
            return;
        }
        if (ExtrudeCutTemplateBuilder.IsMergeFalseTemplate(def))
        {
            ValidateMergeFalseTemplate(def);
            return;
        }
        // Only the evidenced full-sketch Boss/Blind combos are emitted. The supported option
        // slices are documented in TestFiles/Features2026/ExtrudeOptions and every other
        // combination is rejected rather than silently degraded.
        RejectUnsupported(def.EndCondition != 0 && def.EndCondition != 6, nameof(def.EndCondition));
        RejectUnsupported(def.IsCut && def.EndCondition != 0, nameof(def.EndCondition));
        RejectUnsupported(def.IsCut && def.ReverseDirection, nameof(def.ReverseDirection));

        bool twoDirections = def.BothDirections;
        bool draft = def.DraftWhileExtruding;
        bool startOffset = def.FromType == 3;

        if (twoDirections)
        {
            // Evidenced layout: solid Boss, Blind in both directions, forward direction 1,
            // single sketch and no other option.
            RejectUnsupported(def.IsCut, nameof(def.IsCut));
            RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
            RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
            RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
            RejectUnsupported(draft, nameof(def.DraftWhileExtruding));
            RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
            RejectUnsupported(def.FromType != 0, nameof(def.FromType));
            RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
        }
        else
        {
            RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
            RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        }

        if (draft)
        {
            // Evidenced layout: solid Boss Blind, forward direction 1, direction-1 draft only.
            RejectUnsupported(def.IsCut, nameof(def.IsCut));
            RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
            RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
            RejectUnsupported(twoDirections, nameof(def.BothDirections));
            RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
            RejectUnsupported(def.FromType != 0, nameof(def.FromType));
            RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
            if (double.IsNaN(def.DraftAngle) || double.IsInfinity(def.DraftAngle) ||
                def.DraftAngle <= 0.0 || def.DraftAngle >= Math.PI / 2.0)
            {
                throw new ArgumentOutOfRangeException(nameof(def.DraftAngle),
                    $"Draft angle must be a finite value between 0 and pi/2 radians, but received {def.DraftAngle}.");
            }
        }
        else
        {
            RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        }
        RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftAngleDirection2 != 0, nameof(def.DraftAngleDirection2));

        if (startOffset)
        {
            // Evidenced layout: solid Boss Blind, forward direction 1, unflipped start offset.
            RejectUnsupported(def.IsCut, nameof(def.IsCut));
            RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
            RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
            RejectUnsupported(twoDirections, nameof(def.BothDirections));
            RejectUnsupported(draft, nameof(def.DraftWhileExtruding));
            if (double.IsNaN(def.FromOffsetDistance) || double.IsInfinity(def.FromOffsetDistance) ||
                def.FromOffsetDistance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(def.FromOffsetDistance),
                    $"Start offset distance must be a positive finite number, but received {def.FromOffsetDistance}.");
            }
        }
        else
        {
            RejectUnsupported(def.FromType != 0, nameof(def.FromType));
            RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
        }

        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        RejectUnsupported(def.FromOffsetReverse, nameof(def.FromOffsetReverse));
        RejectUnsupported(def.FeatureScope, nameof(def.FeatureScope));
        RejectUnsupported(def.AssemblyFeatureScope, nameof(def.AssemblyFeatureScope));
        RejectUnsupported(def.AutoSelectComponents, nameof(def.AutoSelectComponents));
        RejectUnsupported(def.PropagateFeatureToParts, nameof(def.PropagateFeatureToParts));
        RejectUnsupported(def.FlipSideToCut, nameof(def.FlipSideToCut));
        RejectUnsupported(def.NormalCut, nameof(def.NormalCut));
        RejectUnsupported(def.OptimizeGeometry, nameof(def.OptimizeGeometry));
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(def.CapEnds, nameof(def.CapEnds));
        RejectUnsupported(def.LinkToThickness, nameof(def.LinkToThickness));
        RejectUnsupported(def.TranslateSurface, nameof(def.TranslateSurface));
        RejectUnsupported(def.TranslateSurfaceDirection2, nameof(def.TranslateSurfaceDirection2));
        RejectUnsupported(def.ReverseOffset, nameof(def.ReverseOffset));
        RejectUnsupported(def.ReverseOffsetDirection2, nameof(def.ReverseOffsetDirection2));
        RejectUnsupported(def.AutoFilletCorners, nameof(def.AutoFilletCorners));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.WallThickness != 0, nameof(def.WallThickness));
        RejectUnsupported(def.WallThicknessDirection2 != 0, nameof(def.WallThicknessDirection2));
        RejectUnsupported(def.CapThickness != 0, nameof(def.CapThickness));
        RejectUnsupported(def.AutoFilletRadius != 0, nameof(def.AutoFilletRadius));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        RejectUnsupported(!def.AutoSelect, nameof(def.AutoSelect));

        if (def.SketchSelectionMode == SketchSelectionMode.ExplicitSelection)
        {
            // Measured explicit-selection slices on the controlled dual-rectangle
            // SelectionAuthoring sketch: one/two closed contours or one region (measured
            // 4-edge overlap / 6-edge L-shape), plain Boss Blind at the template depth.
            RejectUnsupported(def.IsCut, nameof(def.IsCut));
            RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
            RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
            RejectUnsupported(def.FromType != 0, nameof(def.FromType));
            RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));

            var selectedContours = def.SelectedContours;
            var selectedRegions = def.SelectedRegions;
            bool hasContours = selectedContours != null && selectedContours.Count > 0;
            bool hasRegions = selectedRegions != null && selectedRegions.Count > 0;
            if (hasContours && hasRegions)
            {
                throw new NotSupportedException(
                    "Explicit selection authoring accepts either SelectedContours or SelectedRegions, not both. " +
                    "No stream changes have been made.");
            }
            if (hasRegions)
            {
                if (selectedRegions!.Count != 1)
                {
                    throw new NotSupportedException(
                        "Region selection authoring supports exactly one selected region. " +
                        "No stream changes have been made.");
                }
                var region = selectedRegions[0];
                if (region == null)
                    throw new ArgumentException("SelectedRegions must not contain null entries.", nameof(def));
                if (region.LoopCount > 1)
                {
                    throw new NotSupportedException(
                        "Regions with holes or multiple boundary loops are not supported. " +
                        "No stream changes have been made.");
                }
                var edges = region.EdgeIds;
                if (edges == null || edges.Count == 0)
                    throw new ArgumentException("The selected region requires non-empty EdgeIds.", nameof(def));
                if (edges.Count != 4 && edges.Count != 6)
                {
                    throw new NotSupportedException(
                        "Region selection authoring supports the measured 4-edge and 6-edge straight-line " +
                        "single loops. No stream changes have been made.");
                }
                if (edges.Any(id => id == 0) || edges.Distinct().Count() != edges.Count)
                    throw new ArgumentException("Region edge ids must be non-zero and distinct.", nameof(def));
            }
            else if (hasContours)
            {
                if (selectedContours!.Count > 2)
                {
                    throw new NotSupportedException(
                        "Explicit contour selection authoring supports one or two contours. " +
                        "No stream changes have been made.");
                }
                foreach (var contour in selectedContours)
                {
                    if (contour == null)
                        throw new ArgumentException("SelectedContours must not contain null entries.", nameof(def));
                    if (contour.IsCandidateOnly)
                    {
                        throw new NotSupportedException(
                            "Candidate-only contours are not accepted for authoring. No stream changes have been made.");
                    }
                    if (contour.EntityIndices == null || contour.EntityIndices.Count == 0)
                        throw new ArgumentException("Each selected contour requires non-empty EntityIndices.", nameof(def));
                    if (contour.EntityIndices.Any(id => id == 0) ||
                        contour.EntityIndices.Distinct().Count() != contour.EntityIndices.Count)
                    {
                        throw new ArgumentException("Contour entity ids must be non-zero and distinct.", nameof(def));
                    }
                }
            }
            else
            {
                throw new NotSupportedException(
                    "Explicit selection authoring requires one or two contours or one region. " +
                    "No stream changes have been made.");
            }

            if (Math.Abs(def.Depth - SelectionExtrudeTemplates.TemplateDepth) > 1e-12)
            {
                throw new NotSupportedException(
                    $"Explicit selection authoring is limited to the measured template depth " +
                    $"{SelectionExtrudeTemplates.TemplateDepth} m. No stream changes have been made.");
            }
        }
        else
        {
            RejectUnsupported(def.SelectedContours != null && def.SelectedContours.Count > 0, nameof(def.SelectedContours));
            RejectUnsupported(def.SelectedRegions != null && def.SelectedRegions.Count > 0, nameof(def.SelectedRegions));
        }
        RejectUnsupported(def.DirectionReference != null, nameof(def.DirectionReference));
        RejectUnsupported(def.EndConditionEntity != null, nameof(def.EndConditionEntity));
        RejectUnsupported(def.BodyScope != null && (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
            (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
            (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)), nameof(def.BodyScope));
    }

    private static void ValidateThinDefinition(ExtrudeFeatureModel def)
    {
        // Measured OneDirection (0), MidPlane (2) and TwoDirection (3) templates.
        RejectUnsupported(def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
        RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
        RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftAngleDirection2 != 0, nameof(def.DraftAngleDirection2));
        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        RejectUnsupported(def.FromType != 0, nameof(def.FromType));
        RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
        RejectUnsupported(def.FromOffsetReverse, nameof(def.FromOffsetReverse));
        RejectUnsupported(def.FeatureScope, nameof(def.FeatureScope));
        RejectUnsupported(def.AssemblyFeatureScope, nameof(def.AssemblyFeatureScope));
        RejectUnsupported(def.AutoSelectComponents, nameof(def.AutoSelectComponents));
        RejectUnsupported(def.PropagateFeatureToParts, nameof(def.PropagateFeatureToParts));
        RejectUnsupported(def.FlipSideToCut, nameof(def.FlipSideToCut));
        RejectUnsupported(def.NormalCut, nameof(def.NormalCut));
        RejectUnsupported(def.OptimizeGeometry, nameof(def.OptimizeGeometry));
        RejectUnsupported(def.LinkToThickness, nameof(def.LinkToThickness));
        RejectUnsupported(def.TranslateSurface, nameof(def.TranslateSurface));
        RejectUnsupported(def.TranslateSurfaceDirection2, nameof(def.TranslateSurfaceDirection2));
        RejectUnsupported(def.ReverseOffset, nameof(def.ReverseOffset));
        RejectUnsupported(def.ReverseOffsetDirection2, nameof(def.ReverseOffsetDirection2));
        RejectUnsupported(def.AutoFilletCorners, nameof(def.AutoFilletCorners));
        RejectUnsupported(def.AutoFilletRadius != 0, nameof(def.AutoFilletRadius));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        RejectUnsupported(!def.AutoSelect, nameof(def.AutoSelect));
        RejectUnsupported(def.CapThickness != 0, nameof(def.CapThickness));
        RejectUnsupported(!IsPositiveFinite(def.WallThickness), nameof(def.WallThickness));
        RejectUnsupported(def.SketchSelectionMode == SketchSelectionMode.ExplicitSelection, nameof(def.SketchSelectionMode));
        RejectUnsupported(def.SelectedContours != null && def.SelectedContours.Count > 0, nameof(def.SelectedContours));
        RejectUnsupported(def.SelectedRegions != null && def.SelectedRegions.Count > 0, nameof(def.SelectedRegions));
        RejectUnsupported(def.DirectionReference != null, nameof(def.DirectionReference));
        RejectUnsupported(def.EndConditionEntity != null, nameof(def.EndConditionEntity));
        RejectUnsupported(def.BodyScope != null && (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
            (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
            (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)), nameof(def.BodyScope));

        switch (def.ThinWallType)
        {
            case 0:
            case 2:
                RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
                RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
                RejectUnsupported(def.WallThicknessDirection2 != 0 &&
                                 Math.Abs(def.WallThicknessDirection2 - 0.001) > 1e-12,
                    nameof(def.WallThicknessDirection2));
                if (def.ThinWallType == 2)
                    RejectUnsupported(def.CapEnds, nameof(def.CapEnds));
                break;
            case 3:
                RejectUnsupported(!def.BothDirections, nameof(def.BothDirections));
                RejectUnsupported(!IsPositiveFinite(def.DepthDirection2), nameof(def.DepthDirection2));
                RejectUnsupported(!IsPositiveFinite(def.WallThicknessDirection2), nameof(def.WallThicknessDirection2));
                RejectUnsupported(def.CapEnds, nameof(def.CapEnds));
                break;
            default:
                throw new NotSupportedException(
                    "Thin extrusion authoring supports ThinWallType OneDirection (0), MidPlane (2) " +
                    "and TwoDirection (3). No stream changes have been made.");
        }
    }

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private static void ValidateDraftDirection2Template(ExtrudeFeatureModel def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(!def.IsCut, nameof(def.IsCut));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        RejectUnsupported(!def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
        RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(Math.Abs(def.Depth - 0.010) > 1e-12, nameof(def.Depth));
        RejectUnsupported(Math.Abs(def.DepthDirection2 - 0.005) > 1e-12, nameof(def.DepthDirection2));
        RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
        RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        RejectUnsupported(!def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        if (double.IsNaN(def.DraftAngleDirection2) || double.IsInfinity(def.DraftAngleDirection2) ||
            def.DraftAngleDirection2 <= 0 || def.DraftAngleDirection2 >= Math.PI / 2.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.DraftAngleDirection2),
                "Direction-2 draft angle must be a finite value between 0 and pi/2 radians.");
        }
        ValidateCutTemplateCommon(def);
    }

    private static void ValidateMergeFalseTemplate(ExtrudeFeatureModel def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(!def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.Merge, nameof(def.Merge));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
        RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(Math.Abs(def.Depth - 0.010) > 1e-12, nameof(def.Depth));
        RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
        RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
        RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftAngleDirection2 != 0, nameof(def.DraftAngleDirection2));
        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        ValidateCutTemplateCommon(def);
    }

    private static void ValidateCutTemplateCommon(ExtrudeFeatureModel def, bool upToSurfaceContext = false)
    {
        RejectUnsupported(def.FromType != 0, nameof(def.FromType));
        RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
        RejectUnsupported(def.FromOffsetReverse, nameof(def.FromOffsetReverse));
        RejectUnsupported(!upToSurfaceContext && def.FeatureScope, nameof(def.FeatureScope));
        RejectUnsupported(def.AssemblyFeatureScope, nameof(def.AssemblyFeatureScope));
        RejectUnsupported(def.AutoSelectComponents, nameof(def.AutoSelectComponents));
        RejectUnsupported(def.PropagateFeatureToParts, nameof(def.PropagateFeatureToParts));
        RejectUnsupported(def.FlipSideToCut, nameof(def.FlipSideToCut));
        RejectUnsupported(def.NormalCut, nameof(def.NormalCut));
        RejectUnsupported(def.OptimizeGeometry, nameof(def.OptimizeGeometry));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.WallThickness != 0, nameof(def.WallThickness));
        RejectUnsupported(def.WallThicknessDirection2 != 0, nameof(def.WallThicknessDirection2));
        RejectUnsupported(def.CapEnds, nameof(def.CapEnds));
        RejectUnsupported(def.CapThickness != 0, nameof(def.CapThickness));
        RejectUnsupported(def.LinkToThickness, nameof(def.LinkToThickness));
        RejectUnsupported(def.TranslateSurface, nameof(def.TranslateSurface));
        RejectUnsupported(def.TranslateSurfaceDirection2, nameof(def.TranslateSurfaceDirection2));
        RejectUnsupported(def.ReverseOffset, nameof(def.ReverseOffset));
        RejectUnsupported(def.ReverseOffsetDirection2, nameof(def.ReverseOffsetDirection2));
        RejectUnsupported(def.AutoFilletCorners, nameof(def.AutoFilletCorners));
        RejectUnsupported(def.AutoFilletRadius != 0, nameof(def.AutoFilletRadius));
        RejectUnsupported(def.SketchSelectionMode == SketchSelectionMode.ExplicitSelection, nameof(def.SketchSelectionMode));
        RejectUnsupported(def.SelectedContours != null && def.SelectedContours.Count > 0, nameof(def.SelectedContours));
        RejectUnsupported(def.SelectedRegions != null && def.SelectedRegions.Count > 0, nameof(def.SelectedRegions));
        RejectUnsupported(def.DirectionReference != null, nameof(def.DirectionReference));
        RejectUnsupported(!upToSurfaceContext && def.EndConditionEntity != null, nameof(def.EndConditionEntity));
        RejectUnsupported(!upToSurfaceContext && def.BodyScope != null &&
            (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
             (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
             (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)), nameof(def.BodyScope));
    }

    private static void ValidateUpToSurfaceTemplate(ExtrudeFeatureModel def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(!def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.EndCondition != 4, nameof(def.EndCondition));
        RejectUnsupported(def.Depth != 0, nameof(def.Depth));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
        RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftAngleDirection2 != 0, nameof(def.DraftAngleDirection2));
        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        ValidateCutTemplateCommon(def, upToSurfaceContext: true);
        if (def.EndConditionEntity != null &&
            (def.EndConditionEntity.EntityType != SelectionEntityType.Face ||
             def.EndConditionEntity.EntityId == 0))
        {
            throw new NotSupportedException(
                "UpToSurface authoring requires a measured Face end-condition entity. " +
                "No stream changes have been made.");
        }
        if (def.BodyScope != null && !def.BodyScope.AutoSelect &&
            (def.BodyScope.BodyIds == null || def.BodyScope.BodyIds.Count != 1))
        {
            throw new NotSupportedException(
                "UpToSurface authoring requires exactly the measured body id in an explicit body scope. " +
                "No stream changes have been made.");
        }
    }

    /// <summary>
    /// Basic Boss/Cut authoring is only evidenced on ResolvedFeatures stream versions 0x70 and
    /// 0x71. The option layouts (D2/D3/D19, extended favorites and moFromEndSpec_c) were
    /// established on 0x70 only. Other layouts are rejected instead of being silently written.
    /// </summary>
    private static void EnsureSupportedStreamVersion(byte[] currentStream, ExtrudeFeatureModel def)
    {
        ushort version = currentStream.Length >= 2 ? BitConverter.ToUInt16(currentStream, 0) : (ushort)0;

        // Flip-side cuts use 0x71; the normal-cut sheet-metal sample uses 0x74. The exact base
        // signature and template StreamVersion are validated by the flag-template builder.
        if (ExtrudeCutFlagFeatureBuilder.IsTemplate(def))
        {
            if (version != 0x71 && version != 0x74)
            {
                throw new NotSupportedException(
                    $"Flip-side/normal cut authoring requires ResolvedFeatures version 0x71 or 0x74, " +
                    $"but the document uses 0x{version:X2}. No stream changes have been made.");
            }
            return;
        }

        if (version != 0x70 && version != 0x71)
        {
            throw new NotSupportedException(
                $"Extrusion authoring is only supported for ResolvedFeatures streams version 0x70/0x71, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }

        // The measured 0x71 cut templates carry both-directions/draft/merge-false/UpToSurface
        // options on version 0x71 itself; their exact base signature is validated by the builders.
        if (ExtrudeCutTemplateBuilder.IsDraftDirection2Template(def) ||
            ExtrudeCutTemplateBuilder.IsMergeFalseTemplate(def) ||
            ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(def))
        {
            if (version != 0x71)
            {
                throw new NotSupportedException(
                    $"This cut option template is only supported on ResolvedFeatures version 0x71, " +
                    $"but the document uses 0x{version:X2}. No stream changes have been made.");
            }
            return;
        }

        bool hasOptions = def.BothDirections || def.DraftWhileExtruding || def.FromType == 3;
        if (hasOptions && version != 0x70)
        {
            throw new NotSupportedException(
                $"Extrusion option authoring is only supported for ResolvedFeatures streams with the evidenced " +
                $"version 0x70, but the document uses 0x{version:X2}. No stream changes have been made.");
        }
    }

    /// <summary>
    /// Locates the 0x67 sketch child-count marker inside the target sketch's trailing range.
    /// A missing marker means the sketch layout is not understood; silently appending the feature
    /// produced native-unloadable documents, so fail fast before touching the stream.
    /// </summary>
    internal static int FindSketchChildCountMarker(byte[] currentStream, FeatureFolder targetSketchFolder, int sketchEndOffset)
    {
        int scanStart = Math.Max(targetSketchFolder.TagOffset, sketchEndOffset - 256);
        int scanEnd = Math.Min(sketchEndOffset, currentStream.Length);
        for (int i = scanEnd - 4; i >= scanStart; i--)
        {
            if (currentStream[i] == 0x67 && currentStream[i + 1] == 0x00 &&
                currentStream[i + 2] == 0x00 && currentStream[i + 3] == 0x00)
            {
                if (i + 8 > currentStream.Length)
                    break;
                return i;
            }
        }

        string sketchName = string.IsNullOrEmpty(targetSketchFolder.DisplayName)
            ? $"<tag {targetSketchFolder.TagOffset}>"
            : targetSketchFolder.DisplayName;
        throw new NotSupportedException(
            $"Cannot author an extrusion on sketch '{sketchName}': the 0x67 sketch child-count marker was not " +
            "found in the expected sketch range. No stream changes have been made.");
    }

    private static void RejectUnsupported(bool unsupported, string property)
    {
        if (unsupported)
            throw new NotSupportedException($"Extrusion authoring does not yet support {property}. No stream changes have been made.");
    }

    internal static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                count++;
                i += pattern.Length - 1;
            }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeOptionEmitter.cs
// ------------------------------------------------------------------------
/// <summary>
/// Emits the measured, structured MFC sub-objects that carry extrusion options which are not
/// part of the basic Boss/Blind payload:
/// <list type="bullet">
/// <item>BothDirections + DepthDirection2 (direction-2 indexed EndSpec / distance display stack).</item>
/// <item>DraftWhileExtruding + DraftAngle (moDisplayAngularDim_c -&gt; AngleDim_c -&gt; moAngleParameter_c).</item>
/// <item>FromType=3 + FromOffsetDistance (extended moFromEndSpec_c with a D19 length parameter).</item>
/// </list>
/// Every option scalar (D2, D3, D19) is written field by field through
/// <see cref="MfcStreamWriter"/>; the segment arrays in <see cref="ExtrudeSegments"/> only carry the
/// surrounding measured class payload/display metadata. Offsets and provenance are documented in
/// SldFileTests/TestFiles/Features2026/ExtrudeOptions/two-direction-evidence.md and OPTION-EVIDENCE.md.
/// </summary>
internal static class ExtrudeOptionEmitter
{
    // A named length parameter starts with u16 schema 0x8004, a WChar name, then this metadata
    // header: double 2.0, u32 FFFFFFFF, u32 0, empty WChar, u16 0. The scalar follows.
    private const ushort ParameterSchema = 0x8004;

    // moDisplayDistanceDim_c / moFeatureDimHandle_c are stored inside the favorite as indexed
    // references to the same payloads used by the primary direction.
    private const int DisplayDimTagLength = 28;
    private const int DisplayDimPayloadLength = 556;
    private const int FeatureDimHandleTagLength = 26;
    private const int FeatureDimHandlePayloadLength = 107;
    private const int DisplayBlockLength = DisplayDimTagLength + DisplayDimPayloadLength +
                                           FeatureDimHandleTagLength + FeatureDimHandlePayloadLength;

    private const int FavoritePrefixLength = 342;          // shared display metadata before option data
    private const int FavoriteRepeatPrefixLength = 436;    // direction-1 display state repeated by 0x8114
    private const int DraftFavoriteTruncatedLength = 452;  // bytes kept before the angular-dim enable slot

    // Indexed class references used by the version-0x70 favorite/fromEndSpec serialization.
    private const ushort SecondDirectionSpecReference = 0x810B;
    private const ushort FeatureDimHandleReference = 0x810D;
    private const ushort ParallelPlaneDimReference = 0x810F;
    private const ushort LengthParameterReference = 0x8111;
    private const ushort RepeatedDisplayReference = 0x8114;

    private static readonly byte[] FavoritePayload = ExtrudeSegments.NativeBossFavoritePayload;
    private static readonly byte[] DisplayDimPayload = Slice(
        ExtrudeSegments.BossDisplayDimBlock, DisplayDimTagLength, DisplayDimPayloadLength);
    private static readonly byte[] FeatureDimHandlePayload = Slice(
        ExtrudeSegments.BossDisplayDimBlock,
        DisplayBlockLength - FeatureDimHandlePayloadLength,
        FeatureDimHandlePayloadLength);
    private static readonly byte[] EmptyWChar = { 0xFF, 0xFE, 0xFF, 0x00 };

    private static byte[] Slice(byte[] source, int start, int count)
    {
        var result = new byte[count];
        Array.Copy(source, start, result, 0, count);
        return result;
    }

    // Measured persistent display markers. They identify the option dimension item inside the
    // favorite object; they are not geometric values and do not depend on the amplitude.
    private static readonly byte[] Direction2FavoriteMarker = { 0x04, 0x00, 0x00, 0x00 };
    private static readonly byte[] DraftFavoriteMarker = { 0x2D, 0x00, 0x5C, 0x00 };
    private static readonly byte[] OffsetFavoriteMarker = { 0x2D, 0x00, 0x5C, 0x00 };
    private static readonly byte[] Direction2FavoriteTailFlags = { 0x01, 0x00, 0x00, 0x01 };

    static ExtrudeOptionEmitter()
    {
        // Guard the measured segment layout. The emitted favorites/fromEndSpec records must stay
        // at the offsets the reader and SolidWorks expect (D2 at +1111, D19 at +679, ...).
        if (FavoritePayload.Length != 512 ||
            ExtrudeSegments.BossDisplayDimBlock.Length != DisplayBlockLength ||
            ExtrudeSegments.NativeBossFromEndSpecPayload.Length != 40 ||
            ExtrudeSegments.Direction2LengthParameterMid1.Length != 64 ||
            ExtrudeSegments.Direction2LengthParameterMid2.Length != 318 ||
            ExtrudeSegments.Direction2LengthParameterMid3.Length != 16 ||
            ExtrudeSegments.Direction2LengthParameterMid4.Length != 130 ||
            ExtrudeSegments.Direction2LengthParameterMid5.Length != 16 ||
            ExtrudeSegments.DraftDisplayAngularDimBlock.Length != 692 ||
            ExtrudeSegments.DraftAngleDimBlock.Length != 20 ||
            ExtrudeSegments.DraftAngleParameterMid1.Length != 530 ||
            ExtrudeSegments.DraftAngleParameterTail.Length != 511 ||
            ExtrudeSegments.StartOffsetLengthParameterMid1.Length != 64 ||
            ExtrudeSegments.StartOffsetLengthParameterMid2.Length != 157 ||
            ExtrudeSegments.StartOffsetLengthParameterMid3.Length != 105 ||
            ExtrudeSegments.StartOffsetLengthParameterMid4.Length != 16 ||
            ExtrudeSegments.StartOffsetLengthParameterMid5.Length != 178 ||
            ExtrudeSegments.StartOffsetLengthParameterMid6.Length != 16)
        {
            throw new InvalidOperationException(
                "Measured extrusion option segments do not have the expected layout. Review ExtrudeSegments.");
        }
    }

    /// <summary>
    /// Writes a moFavoriteHandle_c payload for a two-direction extrusion:
    /// shared prefix, direction-2 display marker, 0x810B second EndSpec reference and its zeroed
    /// Blind header, the mirrored display stack, and the D2 named length parameter.
    /// Produces exactly 2251 bytes.
    /// </summary>
    public static void WriteTwoDirectionFavorite(MfcStreamWriter writer, double depthDirection2)
    {
        writer.WriteMfcTag("moFavoriteHandle_c");
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoritePrefixLength));

        // 94-byte direction-2 favorite header: marker pair, zeroed Blind/no-reverse fields and an
        // empty WChar at +36. All three controlled amplitudes share this layout.
        writer.WriteBytes(Direction2FavoriteMarker);
        writer.WriteBytes(new byte[32]);
        writer.WriteBytes(EmptyWChar);
        writer.WriteBytes(new byte[54]);

        writer.WriteU16(SecondDirectionSpecReference);
        writer.WriteBytes(DisplayDimPayload);
        writer.WriteU16(FeatureDimHandleReference);
        writer.WriteBytes(FeatureDimHandlePayload);
        writer.WriteU16(ParallelPlaneDimReference);
        writer.WriteU16(2);
        writer.WriteU16(0);
        writer.WriteU16(LengthParameterReference);
        WriteDirection2LengthParameter(writer, depthDirection2);

        writer.WriteU16(RepeatedDisplayReference);
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoriteRepeatPrefixLength));

        // 74-byte tail flags of the direction-2 favorite record.
        writer.WriteBytes(Direction2FavoriteTailFlags);
        writer.WriteBytes(new byte[58]);
        writer.WriteU16(1);
        writer.WriteBytes(new byte[10]);
    }

    /// <summary>
    /// Writes the draft favorite payload followed by moDisplayAngularDim_c, AngleDim_c and
    /// moAngleParameter_c. The u32 enable flag lives 24 bytes before the display tag, as required
    /// by the native draft layout. Produces 476 bytes of favorite payload before the display block.
    /// </summary>
    public static void WriteDraftFavoriteAndDimension(MfcStreamWriter writer, double draftAngle)
    {
        writer.WriteMfcTag("moFavoriteHandle_c");
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoritePrefixLength));
        writer.WriteBytes(DraftFavoriteMarker);
        writer.WriteBytes(Slice(FavoritePayload, FavoritePrefixLength + DraftFavoriteMarker.Length, DraftFavoriteTruncatedLength - FavoritePrefixLength - DraftFavoriteMarker.Length));

        // Enable flag + 20 zero bytes immediately before moDisplayAngularDim_c.
        writer.WriteU32(1);
        writer.WriteBytes(new byte[20]);

        writer.WriteBytes(ExtrudeSegments.DraftDisplayAngularDimBlock);
        writer.WriteBytes(ExtrudeSegments.DraftAngleDimBlock);

        writer.WriteMfcTag("moAngleParameter_c");
        writer.WriteU16(ParameterSchema);
        writer.WriteWChar("D3");
        WriteNamedParameterHeader(writer);
        writer.WriteF64(draftAngle);
        writer.WriteBytes(ExtrudeSegments.DraftAngleParameterMid1);
        writer.WriteF64(draftAngle);
        writer.WriteBytes(ExtrudeSegments.DraftAngleParameterTail);
    }

    /// <summary>
    /// Writes the favorite payload used by an offset-start extrusion (the measured marker pair is
    /// the only difference from the forward Blind favorite).
    /// </summary>
    public static void WriteOffsetFavorite(MfcStreamWriter writer)
    {
        writer.WriteMfcTag("moFavoriteHandle_c");
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoritePrefixLength));
        writer.WriteBytes(OffsetFavoriteMarker);
        writer.WriteBytes(Slice(FavoritePayload, FavoritePrefixLength + OffsetFavoriteMarker.Length, FavoritePayload.Length - FavoritePrefixLength - OffsetFavoriteMarker.Length));
    }

    /// <summary>
    /// Writes the complete moFromEndSpec_c data for a start-offset extrusion: the internal
    /// discriminator 5 (API FromType=3), the indexed direction-1 display stack with a D19 named
    /// length parameter, the repeated display reference and the standard 40-byte tail.
    /// Produces exactly 1787 bytes.
    /// </summary>
    public static void WriteOffsetFromEndSpec(MfcStreamWriter writer, double fromOffsetDistance)
    {
        writer.WriteU32(5);
        writer.WriteU16(SecondDirectionSpecReference);
        writer.WriteBytes(DisplayDimPayload);
        writer.WriteU16(FeatureDimHandleReference);
        writer.WriteBytes(FeatureDimHandlePayload);
        writer.WriteU16(ParallelPlaneDimReference);
        writer.WriteU16(2);
        writer.WriteU16(0);
        writer.WriteU16(LengthParameterReference);
        WriteStartOffsetLengthParameter(writer, fromOffsetDistance);

        writer.WriteU16(RepeatedDisplayReference);
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoriteRepeatPrefixLength));
        writer.WriteBytes(ExtrudeSegments.NativeBossFromEndSpecPayload);
    }

    private static void WriteDirection2LengthParameter(MfcStreamWriter writer, double value)
    {
        writer.WriteU16(ParameterSchema);
        writer.WriteWChar("D2");
        WriteNamedParameterHeader(writer);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid1);
        writer.WriteF64(-value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid2);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid3);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid4);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid5);
        writer.WriteF64(value);
        writer.WriteBytes(EmptyWChar);
    }

    private static void WriteStartOffsetLengthParameter(MfcStreamWriter writer, double value)
    {
        writer.WriteU16(ParameterSchema);
        writer.WriteWChar("D19");
        WriteNamedParameterHeader(writer);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid1);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid2);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid3);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid4);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid5);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid6);
        writer.WriteF64(value);
        writer.WriteBytes(EmptyWChar);
    }

    private static void WriteNamedParameterHeader(MfcStreamWriter writer)
    {
        writer.WriteF64(2.0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
        writer.WriteBytes(EmptyWChar);
        writer.WriteU16(0);
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeOptionParser.cs
// ------------------------------------------------------------------------
/// <summary>Fields established by the native extrusion option experiment dated 2026-10-30.</summary>
internal static class ExtrudeOptionParser
{
    // This layout was established for moExtrusion_c, stream 0x70. Do not apply it
    // to Cut's class references or older streams without independent evidence.
    internal static bool? ReadReverseDirection(ushort streamVersion, string className, byte[]? endSpec)
    {
        if (streamVersion != 0x70 || className != "moExtrusion_c" || endSpec == null || endSpec.Length < 24)
            return null;
        if (BitConverter.ToUInt16(endSpec, 0) != 0 ||
            BitConverter.ToUInt32(endSpec, 2) > 1 ||
            BitConverter.ToUInt32(endSpec, 6) != 0 ||
            BitConverter.ToUInt16(endSpec, 14) != 0)
            return null;
        uint reverse = BitConverter.ToUInt32(endSpec, 10);
        return reverse <= 1 ? reverse == 1 : (bool?)null;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Structured binary segment definitions for SolidWorks extrusion features (moExtrusion_c and moICE_c).
/// Replaces monolithic static byte dumps with decomposed MFC 4.2 CArchive sub-object segments.
/// </summary>
internal static class ExtrudeSegments
{
    public static readonly byte[] CutFolderHeaderReserved0 = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0xCA, 0x01, 0x02, 0x40
    };

    public static readonly byte[] FolderHeaderReserved1 = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00
    };

    public static readonly byte[] BossFolderHeaderIndicator = new byte[] {
        0xBF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    public static readonly byte[] CutFolderHeaderIndicator = new byte[] {
        0x1F, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    public static readonly byte[] BossFolderHeaderDates = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x38, 0x4A, 0x00, 0x00, 0x2C, 0xE7, 0x1E, 0x00, 0x38, 0x4A, 0x00, 
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2C, 0xE7, 0x1E, 0x00, 0x00, 0x00
    };

    public static readonly byte[] CutFolderHeaderDates = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x38, 0x4A, 0x00, 0x00, 0x2C, 0xE7, 0x1E, 0x00, 0x38, 0x4A, 0x00, 
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2C, 0xE7, 0x1E, 0x00, 0x00, 0x00
    };

    public static readonly byte[] FolderHeaderSentinel = new byte[] {
        0x00, 0xFF, 0xFF, 0x00, 0x00, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 
        0x80, 0xBF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF
    };

    public static readonly byte[] FolderHeaderTrailer = new byte[] {
        0x02, 0x00, 0xBB, 0xA5, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
        0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00
    };

    public static readonly byte[] BossDescriptorFlags = new byte[] {
        0x01, 0x00, 0x3B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x6A, 0x00, 
        0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 
        0x01, 0x00
    };

    public static readonly byte[] CutDescriptorFlags = new byte[] {
        0x01, 0x00, 0x3B, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x01, 0x00, 
        0x00, 0x00, 0x6B, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00
    };

    public static readonly byte[] BossScopeBlock = ExtrudeAuthoringSegments.BossScopeBlock;

    public static readonly byte[] CutScopeBlock = ExtrudeAuthoringSegments.CutScopeBlock;

    public static readonly byte[] BossBBoxMidSection = ExtrudeAuthoringSegments.BossBBoxMidSection;

    public static readonly byte[] BossBBoxTrailer = ExtrudeAuthoringSegments.BossBBoxTrailer;

    public static readonly byte[] CutBBoxMidSection = ExtrudeAuthoringSegments.CutBBoxMidSection;

    public static readonly byte[] CutBBoxTrailer = ExtrudeAuthoringSegments.CutBBoxTrailer;

    public static readonly byte[] BossDisplayDimBlock = ExtrudeAuthoringSegments.BossDisplayDimBlock;

    public static readonly byte[] CutDisplayDimBlock = ExtrudeAuthoringSegments.CutDisplayDimBlock;

    public static readonly byte[] BossLengthParamPrefix = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 
        0xFF, 0xFE, 0xFF, 0x00, 0x00, 0x00
    };

    public static readonly byte[] BossLengthParamMid1 = ExtrudeAuthoringSegments.BossLengthParamMid1;

    public static readonly byte[] BossLengthParamMid2 = ExtrudeAuthoringSegments.BossLengthParamMid2;

    public static readonly byte[] BossLengthParamMid3 = new byte[] {
        0x08, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00
    };

    public static readonly byte[] BossLengthParamTrailer = new byte[] {
        0xFF, 0xFE, 0xFF, 0x00
    };

    public static readonly byte[] CutLengthParamPrefix = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 
        0xFF, 0xFE, 0xFF, 0x00, 0x00, 0x00
    };

    public static readonly byte[] CutLengthParamMid1 = ExtrudeAuthoringSegments.CutLengthParamMid1;

    public static readonly byte[] CutLengthParamMid2 = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    public static readonly byte[] CutLengthParamMid3 = ExtrudeAuthoringSegments.CutLengthParamMid3;

    public static readonly byte[] CutLengthParamMid4 = new byte[] {
        0x08, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00
    };

    public static readonly byte[] CutLengthParamTrailer = new byte[] {
        0xFF, 0xFE, 0xFF, 0x00
    };

    public static readonly byte[] BossFavoriteAndFromEndSpec = ExtrudeAuthoringSegments.BossFavoriteAndFromEndSpec;

    public static readonly byte[] CutFavoriteAndFromEndSpec = ExtrudeAuthoringSegments.CutFavoriteAndFromEndSpec;


    // ────────────────────────────────────────────────────────────
    // Option-authoring segments measured from controlled native samples
    // (SldFileTests/TestFiles/Features2026/ExtrudeOptions and reverse_workspace).
    // Option-bearing scalars are written by ExtrudeOptionEmitter; these arrays
    // hold only the measured class payload/display metadata around them.
    // ────────────────────────────────────────────────────────────

    /// <summary>moFavoriteHandle_c data from the controlled native forward Blind Boss sample (ExtrudeOptions/extrude_10mm). Option authoring composes direction-2/draft/offset favorites relative to this measured payload.</summary>
    public static readonly byte[] NativeBossFavoritePayload = ExtrudeAuthoringSegments.NativeBossFavoritePayload;

    /// <summary>moFromEndSpec_c data (40 bytes) of the controlled native forward Blind Boss sample; the offset-start layout appends this tail.</summary>
    public static readonly byte[] NativeBossFromEndSpecPayload = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0xF2, 0xCF, 0x17, 0x61, 0x00, 0x00, 0x00, 0x00, 0x59, 0x4A, 0xA7, 0x45,
        0x70, 0xFC, 0x33, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
    };

    /// <summary>Measured direction-2 length parameter segment (ExtrudeOptions/two-direction native controls).</summary>
    public static readonly byte[] Direction2LengthParameterMid1 = ExtrudeAuthoringSegments.Direction2LengthParameterMid1;

    /// <summary>Measured direction-2 length parameter segment (ExtrudeOptions/two-direction native controls).</summary>
    public static readonly byte[] Direction2LengthParameterMid2 = ExtrudeAuthoringSegments.Direction2LengthParameterMid2;

    /// <summary>Measured direction-2 length parameter segment (ExtrudeOptions/two-direction native controls).</summary>
    public static readonly byte[] Direction2LengthParameterMid3 = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    /// <summary>Measured direction-2 length parameter segment (ExtrudeOptions/two-direction native controls).</summary>
    public static readonly byte[] Direction2LengthParameterMid4 = ExtrudeAuthoringSegments.Direction2LengthParameterMid4;

    /// <summary>Measured direction-2 length parameter segment (ExtrudeOptions/two-direction native controls).</summary>
    public static readonly byte[] Direction2LengthParameterMid5 = new byte[] {
        0x08, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00
    };

    /// <summary>moDisplayAngularDim_c class tag and payload from the controlled draft samples; identical at 3 and 6 degrees.</summary>
    public static readonly byte[] DraftDisplayAngularDimBlock = ExtrudeAuthoringSegments.DraftDisplayAngularDimBlock;

    /// <summary>AngleDim_c class tag and payload from the controlled draft samples.</summary>
    public static readonly byte[] DraftAngleDimBlock = new byte[] {
        0xFF, 0xFF, 0x01, 0x00, 0x0A, 0x00, 0x41, 0x6E, 0x67, 0x6C, 0x65, 0x44, 0x69, 0x6D, 0x5F, 0x63,
        0x02, 0x00, 0x00, 0x00
    };

    /// <summary>Measured moAngleParameter_c segment from the controlled draft samples; the angle value is emitted around it.</summary>
    public static readonly byte[] DraftAngleParameterMid1 = ExtrudeAuthoringSegments.DraftAngleParameterMid1;

    /// <summary>Measured moAngleParameter_c segment from the controlled draft samples; the angle value is emitted around it.</summary>
    public static readonly byte[] DraftAngleParameterTail = ExtrudeAuthoringSegments.DraftAngleParameterTail;

    /// <summary>Measured start-offset length parameter segment (ExtrudeOptions/offset native controls).</summary>
    public static readonly byte[] StartOffsetLengthParameterMid1 = ExtrudeAuthoringSegments.StartOffsetLengthParameterMid1;

    /// <summary>Measured start-offset length parameter segment (ExtrudeOptions/offset native controls).</summary>
    public static readonly byte[] StartOffsetLengthParameterMid2 = ExtrudeAuthoringSegments.StartOffsetLengthParameterMid2;

    /// <summary>Measured start-offset length parameter segment (ExtrudeOptions/offset native controls).</summary>
    public static readonly byte[] StartOffsetLengthParameterMid3 = ExtrudeAuthoringSegments.StartOffsetLengthParameterMid3;

    /// <summary>Measured start-offset length parameter segment (ExtrudeOptions/offset native controls).</summary>
    public static readonly byte[] StartOffsetLengthParameterMid4 = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    /// <summary>Measured start-offset length parameter segment (ExtrudeOptions/offset native controls).</summary>
    public static readonly byte[] StartOffsetLengthParameterMid5 = ExtrudeAuthoringSegments.StartOffsetLengthParameterMid5;

    /// <summary>Measured start-offset length parameter segment (ExtrudeOptions/offset native controls).</summary>
    public static readonly byte[] StartOffsetLengthParameterMid6 = new byte[] {
        0x08, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00
    };

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeThinAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured thin-extrusion feature block and stripped-base signature. The block is copied from the
/// native sample starting at the moExtruThin_c class tag through the end of the ResolvedFeatures
/// stream; the base is the same sample truncated at that tag with the header folder count and the
/// 0x67 child-count value zeroed for signature normalization.
/// </summary>
internal sealed class ThinExtrudeAuthoringTemplate
{
    internal ThinExtrudeAuthoringTemplate(string name, string baseSha256, int baseLength,
        ushort streamVersion, int strippedFolderCount, int markerValue, byte[] block)
    {
        Name = name;
        BaseSha256 = baseSha256;
        BaseLength = baseLength;
        StreamVersion = streamVersion;
        StrippedFolderCount = strippedFolderCount;
        MarkerValue = markerValue;
        Block = block;
    }

    internal string Name { get; }
    internal string BaseSha256 { get; }
    internal int BaseLength { get; }
    internal ushort StreamVersion { get; }
    internal int StrippedFolderCount { get; }
    internal int MarkerValue { get; }
    internal byte[] Block { get; }
}

internal static class ExtrudeThinAuthoringTemplates
{
    /// <summary>OneDirection measured template from extrude_thin_one_dir_1mm.resolved.bin.</summary>
    internal static readonly ThinExtrudeAuthoringTemplate OneDirection = new ThinExtrudeAuthoringTemplate(
        "Thin_0_0.001",
        "0927027d10b44fac6c931b2f98e68f81f59183d08f9af644f19ca5b899072866",
        7231,
        0x70,
        19,
        4,
        ExtrudeAuthoringSegments.ThinOneDirection);

    /// <summary>OneDirection + CapEnds measured template from extrude_thin_cap_1mm.resolved.bin.</summary>
    internal static readonly ThinExtrudeAuthoringTemplate CapEnds = new ThinExtrudeAuthoringTemplate(
        "Thin_0_0.001",
        "8b1b4f44d2cd2c9635d77f5b46feb561f77d08af45cee9e525d1da9d92202b69",
        7231,
        0x70,
        19,
        4,
        ExtrudeAuthoringSegments.ThinMidPlane);

    /// <summary>MidPlane measured template from extrude_thin_midplane_1mm.resolved.bin.</summary>
    internal static readonly ThinExtrudeAuthoringTemplate MidPlane = new ThinExtrudeAuthoringTemplate(
        "Thin_2_0.001",
        "0719f8842cf5c9378f91f61a4cba104c121d25fbbee17154cbbd33fecc30d9a9",
        7231,
        0x70,
        19,
        4,
        ExtrudeAuthoringSegments.ThinTwoDirection);

    /// <summary>TwoDirection measured template from extrude_thin_two_dir_1_2mm.resolved.bin.</summary>
    internal static readonly ThinExtrudeAuthoringTemplate TwoDirection = new ThinExtrudeAuthoringTemplate(
        "Thin_3_0.001",
        "54a131d0b427556eb1b5af689ac850059a65c746611db451fc601026911a83b6",
        7231,
        0x70,
        19,
        4,
        ExtrudeAuthoringSegments.ThinOneDirectionResave);

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeThinFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Controlled template authoring for the measured thin extrusion classes.
/// The stripped base must be the exact native sample truncated at moExtruThin_c with the header
/// folder count and the preceding 0x67 value zeroed. The complete native feature block is appended
/// after patching the measured depth/wall slots: OneDirection, OneDirection+CapEnds, MidPlane and
/// TwoDirection each use their own measured block. Other combinations are rejected.
/// </summary>
internal static class ExtrudeThinFeatureBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static ThinExtrudeAuthoringTemplate GetTemplate(ExtrudeFeatureModel definition)
    {
        switch (definition.ThinWallType)
        {
            case 0:
                return definition.CapEnds
                    ? ExtrudeThinAuthoringTemplates.CapEnds
                    : ExtrudeThinAuthoringTemplates.OneDirection;
            case 2:
                return ExtrudeThinAuthoringTemplates.MidPlane;
            case 3:
                return ExtrudeThinAuthoringTemplates.TwoDirection;
            default:
                throw new NotSupportedException(
                    "Thin extrusion templates cover only OneDirection (0), MidPlane (2) and " +
                    "TwoDirection (3). No stream changes have been made.");
        }
    }

    internal static string GetTemplateName(ExtrudeFeatureModel definition) =>
        GetTemplate(definition).Name;

    internal static byte[] Build(
        byte[] currentStream,
        ExtrudeFeatureModel definition,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        var template = GetTemplate(definition);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring uses the measured native feature block/name '{template.Name}'. " +
                $"A custom feature name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (featureName != template.Name || targetSketchFolder == null)
            throw new NotSupportedException("Thin extrusion authoring requires the measured sketch and feature name. No stream changes have been made.");

        if (currentStream == null || currentStream.Length != template.BaseLength)
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring requires the measured stripped base of length {template.BaseLength} " +
                $"bytes; the document stream is {currentStream?.Length ?? 0} bytes. No stream changes have been made.");
        }
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring requires the stripped folder count {template.StrippedFolderCount}. " +
                "No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream.Length < 8 ||
            currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "Thin extrusion authoring requires the stripped base's 0x67 sketch marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream, marker);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        Patch(block, definition);

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static void Patch(byte[] block, ExtrudeFeatureModel definition)
    {
        double depth = definition.Depth;
        double depth2 = definition.DepthDirection2;
        double wall1 = definition.WallThickness;
        double wall2 = definition.WallThicknessDirection2;
        uint wallType = definition.ThinWallType;
        bool twoDirections = wallType == 3;

        var d1 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D1");
        var d5 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D5");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D2");
        var d6 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D6");
        if (d1.Count != 1 || d5.Count != 1 || (twoDirections && (d2.Count != 1 || d6.Count != 1)))
            throw new InvalidOperationException("Measured thin template is missing required parameters.");
        int depthOffset = d1[0].Offset;
        int wallOffset = d5[0].Offset;

        // Layout guard: every slot must hold the measured value before it is patched.
        foreach (int off in new[] { 0x20, 0x40, 0x58, 0x68, 0x250, 0x268 })
            Require(block, depthOffset + off, off == 0x40 || off == 0x58 ? 0.005 : 0.010);
        foreach (int off in new[] { 0x20, 0x1A6, 0x1BE, 0x250, 0x268 })
            Require(block, wallOffset + off, 0.001);
        Require(block, wallOffset + 0x60, wallType == 2 ? -0.0005 : -0.001);

        var tags = MfcPrimitives.ScanMfcTags(block).ToList();
        var radial = tags.Single(t => t.ClassName == "moDisplayRadialDim_c");
        var bbox = tags.Single(t => t.ClassName == "moBBoxCenterData_c");
        Require(block, radial.TagOffset - 24, 0.001);
        double initialY = wallType == 2 ? 0.0 : wallType == 3 ? 0.0005 : -0.0005;
        double initialZ = twoDirections ? 0.0025 : 0.005;
        double initialDiameter = twoDirections ? 0.018275666882497064 : 0.014177446878757825;
        Require(block, bbox.DataStart + 0x0C, initialY);
        Require(block, bbox.DataStart + 0x14, initialZ);
        Require(block, bbox.DataStart + 0x1C, initialDiameter);
        Require(block, bbox.DataStart + 0xEE, initialY);
        Require(block, bbox.DataStart + 0xF6, initialZ);
        Require(block, bbox.DataStart + 0xFE, initialDiameter);

        double halfDepth = depth / 2.0;
        double halfWall = wall1 / 2.0;
        double centerY = wallType == 2 ? 0.0 : twoDirections ? (wall2 - wall1) / 2.0 : -halfWall;
        double centerZ = twoDirections ? (depth - depth2) / 2.0 : halfDepth;
        double halfY = twoDirections ? (wall1 + wall2) / 2.0 : halfWall;
        double halfZ = twoDirections ? (depth + depth2) / 2.0 : halfDepth;
        double diameter = 2.0 * Math.Sqrt(0.005 * 0.005 + halfY * halfY + halfZ * halfZ);

        WriteDouble(block, depthOffset + 0x20, depth);
        WriteDouble(block, depthOffset + 0x40, halfDepth);
        WriteDouble(block, depthOffset + 0x58, halfDepth);
        WriteDouble(block, depthOffset + 0x68, depth);
        WriteDouble(block, depthOffset + 0x250, depth);
        WriteDouble(block, depthOffset + 0x268, depth);

        if (twoDirections)
        {
            int direction2 = d2[0].Offset;
            int wall2Offset = d6[0].Offset;
            foreach (int off in new[] { 0x20, 0x40, 0x58, 0x250, 0x268 })
                Require(block, direction2 + off, 0.005);
            Require(block, direction2 + 0x68, -0.005);
            WriteDouble(block, direction2 + 0x20, depth2);
            WriteDouble(block, direction2 + 0x40, depth2);
            WriteDouble(block, direction2 + 0x58, depth2);
            WriteDouble(block, direction2 + 0x68, -depth2);
            WriteDouble(block, direction2 + 0x250, depth2);
            WriteDouble(block, direction2 + 0x268, depth2);

            foreach (int off in new[] { 0x20, 0x60, 0x1A6, 0x1BE, 0x250, 0x268 })
                Require(block, wall2Offset + off, 0.002);
            foreach (int off in new[] { 0x20, 0x60, 0x1A6, 0x1BE, 0x250, 0x268 })
                WriteDouble(block, wall2Offset + off, wall2);
        }

        WriteDouble(block, wallOffset + 0x20, wall1);
        WriteDouble(block, wallOffset + 0x60, wallType == 2 ? -halfWall : -wall1);
        WriteDouble(block, wallOffset + 0x1A6, wall1);
        WriteDouble(block, wallOffset + 0x1BE, wall1);
        WriteDouble(block, wallOffset + 0x250, wall1);
        WriteDouble(block, wallOffset + 0x268, wall1);
        WriteDouble(block, radial.TagOffset - 24, wall1);

        WriteDouble(block, bbox.DataStart + 0x0C, centerY);
        WriteDouble(block, bbox.DataStart + 0x14, centerZ);
        WriteDouble(block, bbox.DataStart + 0x1C, diameter);
        WriteDouble(block, bbox.DataStart + 0xEE, centerY);
        WriteDouble(block, bbox.DataStart + 0xF6, centerZ);
        WriteDouble(block, bbox.DataStart + 0xFE, diameter);
    }

    private static void Require(byte[] block, int offset, double expected)
    {
        if (offset < 0 || offset + 8 > block.Length ||
            Math.Abs(BitConverter.ToDouble(block, offset) - expected) > 1e-12)
            throw new InvalidOperationException(
                "Measured thin template layout changed; refusing to patch an unknown slot.");
    }

    private static void WriteDouble(byte[] block, int offset, double value)
    {
        if (offset < 0 || offset + 8 > block.Length)
            throw new InvalidOperationException("Measured thin template slot is out of range.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }

    private static string NormalizedBaseSha(byte[] stream, int marker)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        BitConverter.GetBytes(0u).CopyTo(normalized, marker + 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeThinFeatureParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict reader for the measured thin-extrusion class (<c>moExtruThin_c</c>, FolderHeader TypeId 0x52,
/// ResolvedFeatures stream version 0x70).
/// </summary>
internal static class ExtrudeThinFeatureParser
{
    internal const string ClassName = "moExtruThin_c";
    internal const uint TypeId = 0x52;

    private const int DisplayBlockLength = 28;
    private const int CapOffset = 0;
    private const int ThicknessOffset = 4;
    private const int ZeroTailOffset = 12;
    private const int MarkerOffset = 19;
    private const int WallTypeOffset = 20;

    internal static bool TryParse(
        MfcDocument doc,
        FeatureFolder folder,
        ushort streamVersion,
        int start,
        int end,
        out ExtrudeFeatureModel? definition)
    {
        definition = null;
        if (folder == null) return false;

        var ctx = FeatureWindowContext.TryCreate(
            doc,
            folder.ClassName,
            folder.Header.TypeId,
            start,
            end,
            expectedClassName: ClassName,
            expectedTypeId: TypeId,
            expectedStreamVersion: 0x70);
        if (ctx == null || streamVersion != 0x70) return false;

        if (!ctx.HasExactNodeCount("moEndSpec_c", 1) ||
            !ctx.HasExactNodeCount("moThinSpec_c", 1) ||
            !ctx.HasExactNodeCount("moDisplayRadialDim_c", 1) ||
            !ctx.HasExactNodeCount("ThreeDRadiusDim_c", 1) ||
            !ctx.HasExactNodeCount("moFavoriteHandle_c", 1) ||
            !ctx.HasExactNodeCount("moFromEndSpec_c", 1) ||
            !ctx.HasExactNodeCount("ParallelPlaneDistanceDim_c", 1) ||
            !ctx.HasExactNodeCount("moLengthParameter_c", 1))
        {
            return false;
        }

        byte[] raw = ctx.RawData;
        int limit = ctx.End;
        var radial = ctx.GetSingleNode("moDisplayRadialDim_c")!;
        int block = radial.TagOffset - DisplayBlockLength;
        if (block < ctx.Start || radial.TagOffset > limit || block + DisplayBlockLength > limit)
            return false;

        uint cap = BitConverter.ToUInt32(raw, block + CapOffset);
        if (cap > 1) return false;
        double blockThickness = BitConverter.ToDouble(raw, block + ThicknessOffset);
        if (!IsPositiveFinite(blockThickness)) return false;
        for (int i = block + ZeroTailOffset; i < block + MarkerOffset; i++)
            if (raw[i] != 0) return false;
        if (raw[block + MarkerOffset] != 0xC0) return false;
        byte wallType = raw[block + WallTypeOffset];
        if (wallType != 0 && wallType != 2 && wallType != 3) return false;
        for (int i = block + WallTypeOffset + 1; i < radial.TagOffset; i++)
            if (raw[i] != 0) return false;

        var d1 = ctx.Parameters.FindAll("D1");
        var d2 = ctx.Parameters.FindAll("D2");
        var d5 = ctx.Parameters.FindAll("D5");
        var d6 = ctx.Parameters.FindAll("D6");
        var d7 = ctx.Parameters.FindAll("D7");
        if (d1.Count != 1 || d5.Count != 1 || d6.Count != 1) return false;
        if (!IsPositiveFinite(d1[0].Value) || !IsPositiveFinite(d5[0].Value) || !IsPositiveFinite(d6[0].Value))
            return false;
        if (Math.Abs(blockThickness - d5[0].Value) > 1e-12) return false;

        bool autoFillet = false;
        double autoFilletRadius = 0;
        if (end == raw.Length)
        {
            if (raw.Length < 14) return false;
            uint flag = BitConverter.ToUInt32(raw, raw.Length - 14);
            if (flag > 1) return false;
            if (flag == 1)
            {
                if (d7.Count != 1 || !IsPositiveFinite(d7[0].Value)) return false;
                autoFillet = true;
                autoFilletRadius = d7[0].Value;
            }
            else if (d7.Count == 1 && Math.Abs(d7[0].Value) > 1e-12)
            {
                return false;
            }
        }
        else if (d7.Count != 0)
        {
            return false;
        }

        bool twoDirections = wallType == 3;
        double depthDirection2 = 0;
        if (twoDirections)
        {
            if (d2.Count != 1 || !IsPositiveFinite(d2[0].Value)) return false;
            depthDirection2 = d2[0].Value;
        }
        else if (d2.Count != 0)
        {
            return false;
        }

        var endSpec = ctx.GetSingleNode("moEndSpec_c")!.RawData;
        if (endSpec == null || endSpec.Length < 24) return false;
        bool reverseDirection = false;
        if (twoDirections)
        {
            for (int i = 0; i < 24; i++)
                if (endSpec[i] != 0) return false;
        }
        else
        {
            if (BitConverter.ToUInt16(endSpec, 0) != 0 ||
                BitConverter.ToUInt32(endSpec, 2) != 1 ||
                BitConverter.ToUInt32(endSpec, 6) != 0 ||
                BitConverter.ToUInt32(endSpec, 10) > 1 ||
                BitConverter.ToUInt16(endSpec, 14) != 0) return false;
            reverseDirection = BitConverter.ToUInt32(endSpec, 10) == 1;
        }

        definition = new ExtrudeFeatureModel
        {
            IsCut = false,
            IsThinFeature = true,
            ThinWallType = wallType,
            WallThickness = d5[0].Value,
            WallThicknessDirection2 = d6[0].Value,
            CapEnds = cap == 1,
            CapThickness = 0,
            Depth = d1[0].Value,
            DepthDirection2 = depthDirection2,
            BothDirections = twoDirections,
            EndCondition = 0,
            EndConditionDirection2 = 0,
            ReverseDirection = reverseDirection,
            AutoFilletCorners = autoFillet,
            AutoFilletRadius = autoFilletRadius
        };
        string[] resolved =
        {
            nameof(ExtrudeFeatureModel.IsThinFeature),
            nameof(ExtrudeFeatureModel.ThinWallType),
            nameof(ExtrudeFeatureModel.WallThickness),
            nameof(ExtrudeFeatureModel.WallThicknessDirection2),
            nameof(ExtrudeFeatureModel.CapEnds),
            nameof(ExtrudeFeatureModel.Depth),
            nameof(ExtrudeFeatureModel.EndCondition)
        };
        foreach (string name in resolved)
            definition.MarkResolvedInternal(name);
        if (twoDirections)
        {
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.BothDirections));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.DepthDirection2));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.EndConditionDirection2));
        }
        else
        {
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.ReverseDirection));
        }
        if (autoFillet)
        {
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.AutoFilletCorners));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureModel.AutoFilletRadius));
        }
        return true;
    }

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeUpToSurfaceAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured UpToSurface cut block and stripped-base signature. The block includes the complete
/// moICE_c/cut, comp-face, comp-body, FromSktEnt3Int and moSingleFaceRef_w context.
/// </summary>
internal sealed class ExtrudeUpToSurfaceAuthoringTemplate
{
    internal ExtrudeUpToSurfaceAuthoringTemplate(string name, uint faceId, uint bodyId,
        string baseSha256, int baseLength, ushort streamVersion, int strippedFolderCount,
        int markerValue, byte[] block)
    {
        Name = name;
        FaceId = faceId;
        BodyId = bodyId;
        BaseSha256 = baseSha256;
        BaseLength = baseLength;
        StreamVersion = streamVersion;
        StrippedFolderCount = strippedFolderCount;
        MarkerValue = markerValue;
        Block = block;
    }

    internal string Name { get; }
    internal uint FaceId { get; }
    internal uint BodyId { get; }
    internal string BaseSha256 { get; }
    internal int BaseLength { get; }
    internal ushort StreamVersion { get; }
    internal int StrippedFolderCount { get; }
    internal int MarkerValue { get; }
    internal byte[] Block { get; }
}

internal static class ExtrudeUpToSurfaceAuthoringTemplates
{
    /// <summary>source UpToSurface template from extrude_upto_surface_modify.resolved.bin.
    /// FaceId=0x7CB, BodyId=2479.</summary>
    internal static readonly ExtrudeUpToSurfaceAuthoringTemplate ModifySource = new ExtrudeUpToSurfaceAuthoringTemplate(
        "UpToSurfaceModify",
        0x7CBu,
        2479u,
        "7fc8b4112b002081367d3cdb92293561ca3f3affbaed1d9e0b3db2b76c9a85da",
        16330,
        0x71,
        21,
        11,
        ExtrudeAuthoringSegments.UpToSurfaceModifySource);

    /// <summary>native resave UpToSurface template from extrude_upto_surface_modify_resave.resolved.bin.
    /// FaceId=0xAFA, BodyId=2479.</summary>
    internal static readonly ExtrudeUpToSurfaceAuthoringTemplate ModifyResave = new ExtrudeUpToSurfaceAuthoringTemplate(
        "UpToSurfaceModify",
        0xAFAu,
        2479u,
        "15686e08d7714232f3191e764ccf1f890b68bc551680e1ffb495e43f451d435c",
        16757,
        0x71,
        21,
        11,
        ExtrudeAuthoringSegments.UpToSurfaceModifyResave);

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ExtrudeUpToSurfaceTemplateBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Controlled fixed-measurement UpToSurface cut authoring on the extrude_upto_surface_modify source
/// and its native resave bases. The complete measured block carries the target face, explicit body
/// scope and cut context; only the exact measured geometry/target is accepted. OffsetFromSurface,
/// UpToVertex, UpToBody and other end conditions are rejected by the normal cut validation path.
/// </summary>
internal static class ExtrudeUpToSurfaceTemplateBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static bool IsTemplate(ExtrudeFeatureModel definition) =>
        definition.IsCut && !definition.IsThinFeature && definition.EndCondition == 4;

    internal static string GetTemplateName(ExtrudeFeatureModel definition) =>
        ExtrudeUpToSurfaceAuthoringTemplates.ModifySource.Name;

    internal static byte[] Build(
        byte[] currentStream, ExtrudeFeatureModel definition, string featureName)
    {
        var template = SelectByBaseLength(currentStream);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"UpToSurface authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (definition.EndConditionEntity != null)
        {
            if (definition.EndConditionEntity.EntityType != SelectionEntityType.Face ||
                definition.EndConditionEntity.EntityId != template.FaceId)
            {
                throw new NotSupportedException(
                    $"UpToSurface authoring is bound to the measured target face 0x{template.FaceId:X} " +
                    "of this stripped base; a different face id cannot be re-resolved. " +
                    "No stream changes have been made.");
            }
        }
        if (definition.BodyScope?.BodyIds != null && definition.BodyScope.BodyIds.Count > 0)
        {
            if (definition.BodyScope.BodyIds.Count != 1 || definition.BodyScope.BodyIds[0] != template.BodyId)
            {
                throw new NotSupportedException(
                    $"UpToSurface authoring is bound to the measured body scope {template.BodyId} " +
                    "of this stripped base. No stream changes have been made.");
            }
        }
        if (definition.BodyScope?.Bodies != null && definition.BodyScope.Bodies.Count > 0 &&
            definition.BodyScope.Bodies.Any(b => b == null || b.EntityId != template.BodyId))
        {
            throw new NotSupportedException(
                $"UpToSurface authoring is bound to the measured body scope {template.BodyId}. " +
                "No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"UpToSurface authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"UpToSurface authoring requires the stripped folder count {template.StrippedFolderCount}. " +
                "No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "UpToSurface authoring requires the stripped base's 0x67 marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream, marker);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"UpToSurface authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static ExtrudeUpToSurfaceAuthoringTemplate SelectByBaseLength(byte[] currentStream)
    {
        int length = currentStream?.Length ?? 0;
        if (length == ExtrudeUpToSurfaceAuthoringTemplates.ModifySource.BaseLength)
            return ExtrudeUpToSurfaceAuthoringTemplates.ModifySource;
        if (length == ExtrudeUpToSurfaceAuthoringTemplates.ModifyResave.BaseLength)
            return ExtrudeUpToSurfaceAuthoringTemplates.ModifyResave;
        throw new NotSupportedException(
            $"UpToSurface authoring requires the measured stripped base length " +
            $"{ExtrudeUpToSurfaceAuthoringTemplates.ModifySource.BaseLength} or " +
            $"{ExtrudeUpToSurfaceAuthoringTemplates.ModifyResave.BaseLength}; the document stream is " +
            $"{length} bytes. No stream changes have been made.");
    }

    private static string NormalizedBaseSha(byte[] stream, int marker)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        BitConverter.GetBytes(0u).CopyTo(normalized, marker + 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/MfcExtrudeSerializer.cs
// ------------------------------------------------------------------------
/// <summary>
/// Modular parametric MFC serializer for SolidWorks extrusion features (moExtrusion_c and moICE_c).
/// Generates compliant MFC 4.2 CArchive binary structures with dynamic topology linking and variable-length names.
/// </summary>
internal static class MfcExtrudeSerializer
{
    private static readonly byte[] DefaultBodyMagic = new byte[] {
        0x7D, 0xC3, 0x94, 0x25, 0xAD, 0x49, 0xB2, 0x54,
        0x7D, 0xC3, 0x94, 0x25, 0xAD, 0x49, 0xB2, 0x54
    };

    /// <summary>
    /// Extracts the ordered entity IDs of the target sketch chain.
    /// The chain is taken from the resolved folder when available, otherwise it is parsed from the
    /// stream's own <c>moSketchChain_c</c> element. Authoring requires real entity indices; there is
    /// deliberately no [1..4] or byte-pattern fallback, because synthesized IDs bind the extrusion
    /// to non-existent TOPOLOGY entities that native SolidWorks cannot load.
    /// </summary>
    public static List<uint> ExtractEntityIds(FeatureFolder targetSketchFolder, byte[]? stream = null, int sketchEndOffset = -1)
    {
        if (targetSketchFolder == null)
            throw new NotSupportedException("Cannot author an extrusion without a target sketch folder.");

        ushort version = stream != null && stream.Length >= 2 ? BitConverter.ToUInt16(stream, 0) : (ushort)0;
        string sketchName = string.IsNullOrEmpty(targetSketchFolder.DisplayName)
            ? $"<tag {targetSketchFolder.TagOffset}>"
            : targetSketchFolder.DisplayName;

        var chain = targetSketchFolder.ChildFeatureData.OfType<SketchChainData>().FirstOrDefault()
            ?? targetSketchFolder.ChildFeatureDataBuilder.OfType<SketchChainData>().FirstOrDefault();
        var ids = ReadChainEntityIds(chain);
        if (ids != null) return ids;

        // SldPart may resolve the target sketch from a raw stream scan (a bare FeatureFolder
        // without ChildFeatureData). Parse the owning sketch's chain element directly then.
        bool sawChainElement = false;
        if (stream != null && targetSketchFolder.TagOffset >= 0)
        {
            int end = sketchEndOffset > targetSketchFolder.TagOffset
                ? Math.Min(sketchEndOffset, stream.Length)
                : stream.Length;
            var chainTag = MfcPrimitives.ScanMfcTags(stream)
                .FirstOrDefault(t => t.ClassName == "moSketchChain_c" &&
                                     t.TagOffset >= targetSketchFolder.TagOffset &&
                                     t.TagOffset < end);
            if (chainTag != null)
            {
                sawChainElement = true;
                var streamed = new SketchChainData();
                TopologyParsers.ParseEnrichedSketchChain(
                    streamed, MfcPrimitives.CopyRange(stream, chainTag.DataStart, chainTag.DataEnd));
                ids = ReadChainEntityIds(streamed);
                if (ids != null) return ids;
            }
        }

        if (!sawChainElement && chain == null)
        {
            throw new NotSupportedException(
                $"Cannot author an extrusion on sketch '{sketchName}' (ResolvedFeatures version 0x{version:X2}): " +
                "no parseable moSketchChain_c was found for the target sketch. Only sketches with a decoded " +
                "closed chain can be extruded. No stream changes have been made.");
        }

        throw new NotSupportedException(
            $"Cannot author an extrusion on sketch '{sketchName}' (ResolvedFeatures version 0x{version:X2}): " +
            "the moSketchChain_c of the target sketch does not expose any sketch entity indices. " +
            "Synthesized entity ids are not written. No stream changes have been made.");
    }

    private static List<uint>? ReadChainEntityIds(SketchChainData? chain)
    {
        if (chain == null) return null;
        if (chain.EntityIndices != null && chain.EntityIndices.Count > 0)
            return chain.EntityIndices.ToList();
        if (chain.Indices != null && chain.Indices.Any(i => i > 0))
            return chain.Indices.Where(i => i > 0).ToList();
        return null;
    }

    /// <summary>
    /// Synthesizes dynamic sketch topology mapping (moFromSktEntSurfIdRep_c) for Boss Extrude.
    /// Produces exactly 139 + 40N bytes for N contour entities.
    /// </summary>
    public static byte[] SynthesizeFromSktEntSurfIdRep(
        IReadOnlyList<uint> entityIds,
        uint surfaceTag = 0x6AE19B71,
        byte[]? bodyMagic = null)
    {
        if (entityIds == null || entityIds.Count == 0)
            entityIds = new uint[] { 1, 2, 3, 4 };

        int n = entityIds.Count;
        bodyMagic ??= DefaultBodyMagic;

        using var ms = new MemoryStream(139 + 40 * n);
        using var w = new BinaryWriter(ms);

        // 1. Header (31 bytes)
        w.Write((byte)0xFF);
        w.Write((byte)0xFF);
        w.Write((byte)0x01);
        w.Write((byte)0x00);
        byte[] className = Encoding.ASCII.GetBytes("moFromSktEntSurfIdRep_c");
        w.Write((ushort)className.Length);
        w.Write(className);
        w.Write((ushort)0);

        // 2. Set 1: Reversed topological order [e_N, e_{N-1}, ..., e_1]
        for (int i = n - 1; i >= 0; i--)
        {
            w.Write(new byte[] { 0xE7, 0x80, 0xEA, 0x00 });
            w.Write((uint)0x4F); // FeatureTypeId for Extrude
            w.Write(surfaceTag);
            w.Write(entityIds[i]);

            if (i > 0)
            {
                w.Write(new byte[] { 0xEE, 0x80, 0x00, 0x00 });
            }
        }

        // 3. Set 1 Trailer (22 bytes)
        w.Write(new byte[10]);
        w.Write((ushort)0x4A38);
        w.Write(new byte[10]);

        // 4. Body Identifier Block (38 bytes)
        w.Write((ushort)0x80E3);
        w.Write((uint)1);
        w.Write((uint)0);
        w.Write((uint)6);
        w.Write((uint)0x00000300);
        w.Write((uint)0);
        w.Write(bodyMagic, 0, 16);
        w.Write((ushort)0);

        // 5. Set 2 Header & Loop 1 Marker (28 bytes)
        w.Write(new byte[] { 0xE5, 0x80, 0x00, 0x00 });
        w.Write(new byte[] { 0xE7, 0x80, 0xEA, 0x00 });
        w.Write((uint)0x4F);
        w.Write(surfaceTag);
        w.Write((uint)1); // LoopId = 1
        w.Write((uint)0);
        w.Write(new byte[] { 0xEE, 0x80, 0x00, 0x00 });

        // 6. Set 2 Entities: Cyclic Shift Order [e_N, e_1, e_2, ..., e_{N-1}]
        var cycList = new List<uint>(n);
        cycList.Add(entityIds[n - 1]);
        for (int i = 0; i < n - 1; i++)
            cycList.Add(entityIds[i]);

        for (int i = 0; i < n; i++)
        {
            w.Write(new byte[] { 0xE7, 0x80, 0xEA, 0x00 });
            w.Write((uint)0x4F);
            w.Write(surfaceTag);
            w.Write(cycList[i]);

            if (i < n - 1)
            {
                w.Write(new byte[] { 0xEE, 0x80, 0x00, 0x00 });
            }
        }

        // 7. Set 2 Trailer (28 bytes)
        w.Write(new byte[10]);
        w.Write((ushort)0x4A38);
        w.Write(new byte[10]);
        w.Write((ushort)0);
        w.Write((ushort)1);

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Synthesizes dynamic sketch topology mapping (moFromSktEnt3IntSurfIdRep_c) for Cut Extrude.
    /// Produces exactly 169 + 60N bytes for N contour entities (229 bytes for N=1).
    /// </summary>
    public static byte[] SynthesizeFromSktEnt3IntSurfIdRep(
        IReadOnlyList<uint> entityIds,
        uint cutSurfaceTag = 0x6AE19B83,
        uint baseSurfaceTag = 0x6AE19B82,
        byte[]? bodyMagic = null)
    {
        if (entityIds == null || entityIds.Count == 0)
            entityIds = new uint[] { 5 };

        int n = entityIds.Count;
        bodyMagic ??= DefaultBodyMagic;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // 1. Header (35 bytes)
        w.Write((byte)0xFF);
        w.Write((byte)0xFF);
        w.Write((byte)0x01);
        w.Write((byte)0x00);
        byte[] className = Encoding.ASCII.GetBytes("moFromSktEnt3IntSurfIdRep_c");
        w.Write((ushort)className.Length);
        w.Write(className);
        w.Write((ushort)0);

        // 2. Set 1: Cut Extrude Entities [e_N, ..., e_1]
        for (int i = n - 1; i >= 0; i--)
        {
            w.Write(new byte[] { 0xEE, 0x80, 0xF1, 0x00 });
            w.Write((uint)0x56); // FeatureTypeId for Cut Extrude
            w.Write(cutSurfaceTag);
            w.Write(entityIds[i]);
            w.Write(0xFFFFFFFF);

            if (i > 0)
            {
                w.Write(new byte[] { 0xEE, 0x80, 0x00, 0x00 });
            }
        }

        // 3. Set 1 Trailer (26 bytes)
        w.Write(new byte[14]);
        w.Write((ushort)0x4A38);
        w.Write(new byte[10]);

        // 4. Body Identifier Block (38 bytes)
        w.Write((ushort)0x80EA);
        w.Write((uint)1);
        w.Write((uint)0);
        w.Write((uint)5);
        w.Write((uint)0x00000300);
        w.Write((uint)0x0000289E);
        w.Write(bodyMagic, 0, 16);
        w.Write((ushort)0);

        // 5. Set 2: Cut Loop & Intersection Reference
        var cycList = new List<uint>(n);
        cycList.Add(entityIds[n - 1]);
        for (int i = 0; i < n - 1; i++)
            cycList.Add(entityIds[i]);

        for (int i = 0; i < n; i++)
        {
            w.Write(new byte[] { 0x75, 0x81, 0x00, 0x00 });
            w.Write(new byte[] { 0xEE, 0x80, 0xF1, 0x00 });
            w.Write((uint)0x56);
            w.Write(cutSurfaceTag);
            w.Write(cycList[i]);
            w.Write(0xFFFFFFFF);
            w.Write((uint)0); // 4-byte padding
        }

        // Loop 1 marker
        w.Write(new byte[] { 0x72, 0x81, 0x00, 0x00 });
        w.Write(new byte[] { 0xEE, 0x80, 0xF1, 0x00 });
        w.Write((uint)0x56);
        w.Write(cutSurfaceTag);
        w.Write((uint)1); // loopId = 1
        w.Write(new byte[10]);

        // Target Base Extrude face intersection link
        w.Write(new byte[] { 0xEC, 0x80, 0x00, 0x00 });
        w.Write(new byte[] { 0xEE, 0x80, 0xF1, 0x00 });
        w.Write((uint)0x4F); // Base Extrude TypeId
        w.Write(baseSurfaceTag);
        w.Write((uint)1); // target face 1
        w.Write(new byte[14]);
        w.Write((ushort)0x4A38);
        w.Write(new byte[10]);
        w.Write((ushort)0);
        w.Write((ushort)1);

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Serializes a complete Boss Extrude (moExtrusion_c) binary block using modular parametric MFC streaming.
    /// Variable-length feature names and arbitrary sketch topology are encoded without static offset patching.
    /// </summary>
    public static byte[] SerializeBossExtrude(
        ExtrudeFeatureModel def,
        string featureName,
        IReadOnlyList<uint> entityIndices,
        uint surfaceTag = 0x6AE19B71,
        uint uniqueFeatureId = 0xC28F06D1)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (string.IsNullOrEmpty(featureName))
            featureName = "凸台-拉伸1";

        if (entityIndices == null || entityIndices.Count == 0)
            entityIndices = new uint[] { 1, 2, 3, 4 };

        using var writer = new MfcStreamWriter(3221);

        // 1. Folder Header
        writer.WriteMfcTag("moExtrusion_c");
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0006103515625);
        writer.WriteU32(0x4F);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(ExtrudeSegments.FolderHeaderReserved1);
        writer.WriteBytes(ExtrudeSegments.BossFolderHeaderIndicator);
        writer.WriteU32(0x9E); // MarkerE
        writer.WriteBytes(ExtrudeSegments.BossFolderHeaderDates);
        writer.WriteWChar(string.Empty); // Delimiter
        writer.WriteBytes(ExtrudeSegments.FolderHeaderSentinel);
        writer.WriteU32(0x01DD66A8); // Part Magic
        writer.WriteU32(uniqueFeatureId); // FeatureId
        writer.WriteBytes(ExtrudeSegments.FolderHeaderTrailer);
        writer.WriteBytes(ExtrudeSegments.BossDescriptorFlags);

        // 2. Body Selection Scope
        writer.WriteBytes(ExtrudeSegments.BossScopeBlock);

        // 3. Dynamic Sketch Topology
        byte[] topo = SynthesizeFromSktEntSurfIdRep(entityIndices, surfaceTag);
        writer.WriteBytes(topo);

        // 4. Bounding Box
        // Native controls: MidPlane is centered; both directions span [-D2, +D1]; an offset start
        // shifts the box by the offset distance. The two-direction radius covers D1 + D2.
        double centerZ;
        double radius;
        if (def.EndCondition == 6)
        {
            centerZ = 0.0;
            radius = Math.Sqrt(0.0002 + def.Depth * def.Depth);
        }
        else if (def.BothDirections)
        {
            centerZ = (def.Depth - def.DepthDirection2) / 2.0;
            double total = def.Depth + def.DepthDirection2;
            radius = Math.Sqrt(0.0002 + total * total);
        }
        else if (def.FromType == 3)
        {
            centerZ = def.Depth / 2.0 + def.FromOffsetDistance;
            radius = Math.Sqrt(0.0002 + def.Depth * def.Depth);
        }
        else
        {
            centerZ = def.ReverseDirection ? -def.Depth / 2.0 : def.Depth / 2.0;
            radius = Math.Sqrt(0.0002 + def.Depth * def.Depth);
        }

        writer.WriteMfcTag("moBBoxCenterData_c");
        writer.WriteU16(1);
        writer.WriteU16(0);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(centerZ);
        writer.WriteF64(radius);
        writer.WriteBytes(ExtrudeSegments.BossBBoxMidSection);
        writer.WriteU16(0x8102);
        writer.WriteU16(1);
        writer.WriteU16(0);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(centerZ);
        writer.WriteF64(radius);
        writer.WriteBytes(ExtrudeSegments.BossBBoxTrailer);

        // 5. End Condition
        writer.WriteMfcTag("moEndSpec_c");
        writer.WriteU16(0);
        // u32 at +2 is 1 for a single direction and 0 for the evidenced two-direction layout.
        writer.WriteU32(def.BothDirections ? 0u : 1u);
        writer.WriteU32(0);
        writer.WriteU32(def.ReverseDirection ? 1u : 0u);
        writer.WriteU16(0);
        writer.WriteU32(def.EndCondition);
        writer.WriteU32(0);

        // 6. Display Dimension & Feature Dim Handle
        writer.WriteBytes(ExtrudeSegments.BossDisplayDimBlock);

        // 7. ParallelPlaneDistanceDim_c & moLengthParameter_c
        writer.WriteMfcTag("ParallelPlaneDistanceDim_c");
        writer.WriteU16(2);
        writer.WriteU16(0);
        writer.WriteMfcTag("moLengthParameter_c");
        writer.WriteU16(0x8004);
        writer.WriteWChar("D1");
        writer.WriteBytes(ExtrudeSegments.BossLengthParamPrefix);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.BossLengthParamMid1);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.BossLengthParamMid2);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.BossLengthParamMid3);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.BossLengthParamTrailer);

        // 8. FavoriteHandle & FromEndSpec
        // The final 40-byte moFromEndSpec_c record repeats the direction flag at +8.
        // Native baseline/resave/reverse controls establish this layout. Option-bearing
        // favorites are composed from the measured option segments; unsupported combinations
        // are rejected by ExtrudeFeatureBuilder.ValidateDefinition before reaching this point.
        if (def.BothDirections)
        {
            ExtrudeOptionEmitter.WriteTwoDirectionFavorite(writer, def.DepthDirection2);
            WriteFromEndSpec(writer, def.ReverseDirection);
        }
        else if (def.DraftWhileExtruding)
        {
            ExtrudeOptionEmitter.WriteDraftFavoriteAndDimension(writer, def.DraftAngle);
            WriteFromEndSpec(writer, false);
        }
        else if (def.FromType == 3)
        {
            ExtrudeOptionEmitter.WriteOffsetFavorite(writer);
            writer.WriteMfcTag("moFromEndSpec_c");
            ExtrudeOptionEmitter.WriteOffsetFromEndSpec(writer, def.FromOffsetDistance);
        }
        else
        {
            var favorite = ExtrudeSegments.BossFavoriteAndFromEndSpec;
            const int fromEndRecordLength = 6 + 15 + 40; // MFC tag + ASCII class name + fields
            var prefix = new byte[favorite.Length - fromEndRecordLength];
            Array.Copy(favorite, prefix, prefix.Length);
            writer.WriteBytes(prefix);
            WriteFromEndSpec(writer, def.ReverseDirection);
        }

        return writer.ToArray();
    }

    private static void WriteFromEndSpec(MfcStreamWriter writer, bool reverse)
    {
        writer.WriteMfcTag("moFromEndSpec_c");
        writer.WriteU32(0);
        writer.WriteU32(0x6117CFF2);
        writer.WriteU32(reverse ? 1u : 0u);
        writer.WriteU32(0x45A74A59);
        writer.WriteU32(0x033F4370);
        writer.WriteU32(0);
        writer.WriteU32(0);
        writer.WriteU32(0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
    }

    /// <summary>
    /// Serializes a complete Cut Extrude (moICE_c) binary block using modular parametric MFC streaming.
    /// Variable-length feature names and arbitrary sketch topology are encoded without static offset patching.
    /// </summary>
    public static byte[] SerializeCutExtrude(
        ExtrudeFeatureModel def,
        string featureName,
        IReadOnlyList<uint> entityIndices,
        uint cutSurfaceTag = 0x6AE19B83,
        uint baseSurfaceTag = 0x6AE19B82,
        uint uniqueFeatureId = 0xCD0C81A9)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (string.IsNullOrEmpty(featureName))
            featureName = "切除-拉伸1";

        if (entityIndices == null || entityIndices.Count == 0)
            entityIndices = new uint[] { 5 };

        using var writer = new MfcStreamWriter(3092);

        // 1. Folder Header
        writer.WriteMfcTag("moICE_c");
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteBytes(ExtrudeSegments.CutFolderHeaderReserved0);
        writer.WriteU32(0x56);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(ExtrudeSegments.FolderHeaderReserved1);
        writer.WriteBytes(ExtrudeSegments.CutFolderHeaderIndicator);
        writer.WriteU32(0xB3); // MarkerE
        writer.WriteBytes(ExtrudeSegments.CutFolderHeaderDates);
        writer.WriteWChar(string.Empty); // Delimiter
        writer.WriteBytes(ExtrudeSegments.FolderHeaderSentinel);
        writer.WriteU32(0x01DD66A8); // Part Magic
        writer.WriteU32(uniqueFeatureId); // FeatureId
        writer.WriteBytes(ExtrudeSegments.FolderHeaderTrailer);
        writer.WriteBytes(ExtrudeSegments.CutDescriptorFlags);

        // 2. Body Selection Scope
        writer.WriteBytes(ExtrudeSegments.CutScopeBlock);

        // 3. Dynamic Sketch Topology
        byte[] topo = SynthesizeFromSktEnt3IntSurfIdRep(entityIndices, cutSurfaceTag, baseSurfaceTag);
        writer.WriteBytes(topo);

        // 4. Bounding Box
        double radius = Math.Sqrt(0.0003); // Bounding radius of 10mm base cube (0.017320508...)

        writer.WriteU16(0x8109);
        writer.WriteU16(1);
        writer.WriteU16(0);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(radius);
        writer.WriteBytes(ExtrudeSegments.CutBBoxMidSection);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(radius);
        writer.WriteBytes(ExtrudeSegments.CutBBoxTrailer);

        // 5. End Condition
        writer.WriteU16(0x8110);
        writer.WriteU16(0);
        writer.WriteU32(1);
        writer.WriteU32(0);
        writer.WriteU32(0);
        writer.WriteU16(0);
        writer.WriteU32(def.EndCondition);
        writer.WriteU32(0);

        // 6. Display Dimension & Feature Dim Handle
        writer.WriteBytes(ExtrudeSegments.CutDisplayDimBlock);

        // 7. ParallelPlaneDistanceDim_c & moLengthParameter_c
        writer.WriteU16(0x8116);
        writer.WriteU16(2);
        writer.WriteU16(0);
        writer.WriteU16(0x8118);
        writer.WriteU16(0x8004);
        writer.WriteWChar("D1");
        writer.WriteBytes(ExtrudeSegments.CutLengthParamPrefix);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamMid1);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamMid2);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamMid3);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamMid4);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamTrailer);

        // 8. FavoriteHandle & FromEndSpec
        writer.WriteBytes(ExtrudeSegments.CutFavoriteAndFromEndSpec);

        return writer.ToArray();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/RegionSelectionExtrudeBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Controlled region-selection extrusion authoring on the measured SelectionAuthoring
/// L-shape (6 edges) and overlap-rectangle (4 edges) bases. The target stream must be
/// the native region part stripped at its extrusion tag: the sketch, the complete
/// region context (moOneContour_c ... moBBoxCenterData_c) and the chooser are retained.
/// The context is re-emitted from the captured RegionChooserTemplate with the loop order
/// derived from the target sketch; the measured region-specific feature block is appended.
/// </summary>
internal static class RegionSelectionExtrudeBuilder
{
    private const string ChooserClass = "moContourChooser_c";
    private const string ExtrudeClass = "moExtrusion_c";
    private const string CutExtrudeClass = "moICE_c";

    internal static byte[] Build(
        byte[] currentStream,
        ExtrudeFeatureModel def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName,
        string? targetDocumentPath,
        SketchFeatureModel? targetSketch)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));
        if (def.SelectedContours != null && def.SelectedContours.Count > 0)
        {
            throw new NotSupportedException(
                "Region selection authoring does not accept SelectedContours. No stream changes have been made.");
        }
        if (!CStringHandlePathPolicy.TryNormalizeDocumentPath(targetDocumentPath, out string normalizedTarget))
        {
            throw new ArgumentException(
                "A valid absolute target document path is required for region selection authoring. " +
                "No stream changes have been made.", nameof(targetDocumentPath));
        }
        if (sketchEndOffset != currentStream.Length)
        {
            throw new NotSupportedException(
                "Region selection authoring requires the extrusion-stripped SelectionAuthoring base whose " +
                "region context/chooser ends at the stream end. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x70 && version != 0x71)
        {
            throw new NotSupportedException(
                $"Region selection authoring requires ResolvedFeatures version 0x70/0x71, but the document " +
                $"uses 0x{version:X2}. No stream changes have been made.");
        }

        var doc = MfcDocument.Parse(currentStream);
        var chooser = FindChooser(doc, targetSketchFolder.TagOffset, sketchEndOffset);
        EnsureNoExistingExtrusion(doc, chooser.TagOffset);
        if (doc.AllElements.Any(e => e.TagOffset >= chooser.TagOffset &&
                                     (e.ClassName == ExtrudeClass || e.ClassName == CutExtrudeClass)))
        {
            throw new NotSupportedException(
                "Region selection authoring requires an extrusion-stripped base; the target still contains " +
                "an extrusion after the region chooser. No stream changes have been made.");
        }

        var template = RegionChooserTemplate.Capture(doc, targetSketchFolder.TagOffset, sketchEndOffset);
        if (template == null)
        {
            throw new NotSupportedException(
                "The target document does not contain a complete native region context/chooser " +
                "(moContourChooser_c plus moOneContour_c/moEdgeRef_c/CStringHandle/trim/bbox tags). " +
                "No stream changes have been made.");
        }
        if (template.ContextTags.Count == 0 || template.ContextPathAmbiguous ||
            string.IsNullOrEmpty(template.SourceDocumentPath))
        {
            throw new NotSupportedException(
                "The captured region context has no readable/unique moCStringHandle_c source path; the writer " +
                "refuses to emit the context. No stream changes have been made.");
        }
        if (!template.OrderingRuleVerified)
        {
            throw new NotSupportedException(
                "The captured region groups do not follow the established boundary-loop ordering rule; " +
                "re-emission would be ambiguous. No stream changes have been made.");
        }
        if (targetSketch == null)
        {
            throw new NotSupportedException(
                "The target sketch geometry is unavailable, so the region boundary loop order cannot be " +
                "derived. No stream changes have been made.");
        }

        var region = ValidateRequestedRegion(def, template);
        string baseSha = ComputeNormalizedStreamSha(currentStream);
        byte[] headerRest;
        byte[] tail;
        int featureIdOffset;
        uint[] expectedNativeEdges;
        if (string.Equals(baseSha, RegionSelectionExtrudeTemplates.LShapeBaseSha256, StringComparison.Ordinal))
        {
            headerRest = RegionSelectionExtrudeTemplates.LShapeHeaderRest;
            tail = RegionSelectionExtrudeTemplates.LShapeTail;
            featureIdOffset = RegionSelectionExtrudeTemplates.LShapeFeatureIdOffset;
            expectedNativeEdges = RegionSelectionExtrudeTemplates.LShapeNativeEdges;
        }
        else if (string.Equals(baseSha, RegionSelectionExtrudeTemplates.OverlapBaseSha256, StringComparison.Ordinal))
        {
            headerRest = RegionSelectionExtrudeTemplates.OverlapHeaderRest;
            tail = RegionSelectionExtrudeTemplates.OverlapTail;
            featureIdOffset = RegionSelectionExtrudeTemplates.OverlapFeatureIdOffset;
            expectedNativeEdges = RegionSelectionExtrudeTemplates.OverlapNativeEdges;
        }
        else
        {
            throw new NotSupportedException(
                $"Region selection authoring is limited to the controlled SelectionAuthoring L-shape/overlap " +
                $"bases (base signature {baseSha.Substring(0, 12)} does not match). No stream changes have been made.");
        }

        if (template.GroupCount != expectedNativeEdges.Length ||
            !SetEquals(template.NativeEdgeIds, expectedNativeEdges))
        {
            throw new NotSupportedException(
                "The captured region chooser does not match the measured edge set of the controlled base. " +
                "No stream changes have been made.");
        }

        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        if (folderCount != RegionSelectionExtrudeTemplates.ExpectedStrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Region selection authoring requires the extrusion-stripped base with folder count " +
                $"{RegionSelectionExtrudeTemplates.ExpectedStrippedFolderCount}, but the document uses " +
                $"{folderCount}. No stream changes have been made.");
        }

        byte[] contextBlock;
        try
        {
            contextBlock = SketchSelectionWriter.BuildRegionSelectionContextFromSketch(
                region.EdgeIds, template, targetSketch, normalizedTarget);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            throw new NotSupportedException(
                "The region boundary loop order/context could not be derived from the target sketch: " +
                ex.Message + " No stream changes have been made.");
        }

        uint featureId = 0xC28F06D1u + (uint)ExtrudeFeatureBuilder.CountOccurrences(currentStream, ExtrudeClass);
        byte[] featureBlock = BuildFeatureBlock(featureName, headerRest, tail, featureId, featureIdOffset);

        int contextStart = template.ContextTags[0].TagOffset;
        if (contextStart <= 0 || contextStart >= currentStream.Length)
        {
            throw new NotSupportedException(
                "The captured region context start is not inside the controlled base. No stream changes have been made.");
        }

        byte[] newStream = new byte[contextStart + contextBlock.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, contextStart);
        Array.Copy(contextBlock, 0, newStream, contextStart, contextBlock.Length);
        Array.Copy(featureBlock, 0, newStream, contextStart + contextBlock.Length, featureBlock.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        return newStream;
    }

    private static MfcElement FindChooser(MfcDocument doc, int sketchStart, int sketchEnd)
    {
        var choosers = doc.AllElements
            .Where(e => e.ClassName == ChooserClass && e.TagOffset >= sketchStart && e.TagOffset < sketchEnd)
            .OrderBy(e => e.TagOffset)
            .ToList();
        if (choosers.Count != 1)
        {
            throw new NotSupportedException(
                $"Region selection authoring requires exactly one complete {ChooserClass} context after the " +
                $"target sketch, but found {choosers.Count}. The target must be the extrusion-stripped region " +
                "SelectionAuthoring base. No stream changes have been made.");
        }
        return choosers[0];
    }

    private static void EnsureNoExistingExtrusion(MfcDocument doc, int windowStart)
    {
        if (doc.AllElements.Any(e => e.TagOffset >= windowStart &&
                                     (e.ClassName == ExtrudeClass || e.ClassName == CutExtrudeClass)))
        {
            throw new NotSupportedException(
                "Region selection authoring currently supports only a base without any extrusion feature. " +
                "No stream changes have been made.");
        }
    }

    private static SketchRegionReference ValidateRequestedRegion(
        ExtrudeFeatureModel def, RegionChooserTemplate template)
    {
        var regions = def.SelectedRegions;
        if (regions == null || regions.Count != 1)
        {
            throw new NotSupportedException(
                "Region selection authoring supports exactly one selected region. No stream changes have been made.");
        }
        var region = regions[0];
        if (region == null)
            throw new ArgumentException("SelectedRegions must not contain null entries.", nameof(def));
        if (region.LoopCount > 1)
        {
            throw new NotSupportedException(
                "Regions with holes or multiple boundary loops are not supported (single straight-line loop only). " +
                "No stream changes have been made.");
        }
        var edges = region.EdgeIds;
        if (edges == null || edges.Count == 0)
            throw new ArgumentException("The selected region requires non-empty EdgeIds.", nameof(def));
        if (edges.Count != 4 && edges.Count != 6)
        {
            throw new NotSupportedException(
                $"Region selection authoring supports the measured 4-edge and 6-edge straight-line single loops, " +
                $"but received {edges.Count} edges. No stream changes have been made.");
        }
        if (edges.Any(id => id == 0) || edges.Distinct().Count() != edges.Count)
            throw new ArgumentException("Region edge ids must be non-zero and distinct.", nameof(def));
        if (edges.Count != template.GroupCount || !SetEquals(edges, template.NativeEdgeIds))
        {
            throw new NotSupportedException(
                $"The requested region edges [{string.Join(",", edges)}] do not match the measured boundary " +
                $"set [{string.Join(",", template.NativeEdgeIds)}] of the controlled base. " +
                "No stream changes have been made.");
        }
        return region;
    }

    private static bool SetEquals(IEnumerable<uint> a, IEnumerable<uint> b) =>
        a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x));

    private static string ComputeNormalizedStreamSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static byte[] BuildFeatureBlock(
        string featureName, byte[] headerRestTemplate, byte[] tail, uint featureId,
        int featureIdOffset)
    {
        byte[] headerRest = (byte[])headerRestTemplate.Clone();
        BitConverter.GetBytes(featureId).CopyTo(headerRest, featureIdOffset);
        using var writer = new MfcStreamWriter(4096);
        writer.WriteMfcTag(ExtrudeClass);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteBytes(headerRest);
        writer.WriteBytes(tail);
        return writer.ToArray();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/RegionSelectionExtrudeTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured region-selection extrusion templates from the native SelectionAuthoring
/// L-shape (6 edges) and overlap-rectangle (4 edges) samples (SW2026, 0x70).
/// A region feature block has no moBBoxCenterData_c; the sketch-wide region context
/// and chooser precede the feature. Templates are emitted only on the exact
/// extrusion-stripped controlled base pinned by the SHA-256 signature.
/// </summary>
internal static class RegionSelectionExtrudeTemplates
{
    internal const double TemplateDepth = 0.010;
    internal const ushort ExpectedStrippedFolderCount = 19;

    internal const string LShapeBaseSha256 =
        "cdb229321d186d29e45a5220258fc11ed6a57761dac54d35de898af1147bcf4d";
    internal const string OverlapBaseSha256 =
        "60bd31482b61cbb52cf371bc5fc0312f7fc80ec63cd8226fe2db6ace8c71b746";

    internal static readonly uint[] LShapeNativeEdges = { 3, 4, 5, 6, 7, 8 };
    internal static readonly uint[] OverlapNativeEdges = { 3, 4, 5, 6 };

    internal const int LShapeFeatureIdOffset = 131;
    internal const int OverlapFeatureIdOffset = 131;

    /// <summary>moExtrusion_c header tail after the WChar feature name (L-shape/6-edge region).</summary>
    internal static readonly byte[] LShapeHeaderRest = SelectionAuthoringSegments.RegionLShapeHeaderRest;

    /// <summary>L-shape region feature objects after the header (scope through moFromEndSpec_c).</summary>
    internal static readonly byte[] LShapeTail = SelectionAuthoringSegments.RegionLShapeTail;

    /// <summary>moExtrusion_c header tail after the WChar feature name (overlap/4-edge region).</summary>
    internal static readonly byte[] OverlapHeaderRest = SelectionAuthoringSegments.RegionOverlapHeaderRest;

    /// <summary>Overlap region feature objects after the header (scope through moFromEndSpec_c).</summary>
    internal static readonly byte[] OverlapTail = SelectionAuthoringSegments.RegionOverlapTail;

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SelectionExtrudeTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured explicit-contour selection extrusion templates from the native
/// SelectionAuthoring samples (SW2026, ResolvedFeatures 0x70/0x71). The templates
/// are emitted only on the matching controlled dual-rectangle sketch prefix; the
/// selection window is rebuilt from the captured suffix and the requested contours.
/// </summary>
internal static class SelectionExtrudeTemplates
{
    internal const double TemplateDepth = 0.010;
    internal const ushort ExpectedStrippedFolderCount = 19;
    internal const ushort SingleContourSurfaceId = 0xBF;
    internal const ushort TwoContourSurfaceId = 0xC0;

    internal const string OneContourPrefixSha256 =
        "9dae813eae280572f3654a7089c9eeb99a1fdf90bf0985c9f07046ef79538995";
    internal const string TwoContourPrefixSha256 =
        "a5a3d1ca62daea0f2ff93bd23506878e87acd10a11acc402e8f019095aabf80d";

    internal const int OneContourFeatureIdOffset = 131;
    internal const int TwoContourFeatureIdOffset = 131;

    /// <summary>moExtrusion_c header tail after the WChar feature name (single selected contour).</summary>
    internal static readonly byte[] OneContourHeaderRest = SelectionAuthoringSegments.SelectionOneContourHeaderRest;

    /// <summary>Single-contour selection extrusion objects after the header (scope through moFromEndSpec_c).</summary>
    internal static readonly byte[] OneContourTail = SelectionAuthoringSegments.SelectionOneContourTail;

    /// <summary>moExtrusion_c header tail after the WChar feature name (two selected contours).</summary>
    internal static readonly byte[] TwoContourHeaderRest = SelectionAuthoringSegments.SelectionTwoContourHeaderRest;

    /// <summary>Two-contour selection extrusion objects after the header (scope through moFromEndSpec_c).</summary>
    internal static readonly byte[] TwoContourTail = SelectionAuthoringSegments.SelectionTwoContourTail;

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SurfaceExtrudeAuthoringTemplates.cs
// ------------------------------------------------------------------------
internal sealed class SurfaceExtrudeAuthoringTemplate
{
    internal SurfaceExtrudeAuthoringTemplate(string variant, string name, string className,
        string baseSha256, int baseLength, ushort streamVersion, int strippedFolderCount,
        int markerOffset, uint markerValue, int depthSlot1, int depthSlot2, double measuredDepth, byte[] block)
    {
        Variant = variant; Name = name; ClassName = className;
        BaseSha256 = baseSha256; BaseLength = baseLength; StreamVersion = streamVersion;
        StrippedFolderCount = strippedFolderCount; MarkerOffset = markerOffset; MarkerValue = markerValue;
        DepthSlot1 = depthSlot1; DepthSlot2 = depthSlot2; MeasuredDepth = measuredDepth;
        Block = block;
    }
    internal string Variant { get; }
    internal string Name { get; }
    internal string ClassName { get; }
    internal string BaseSha256 { get; }
    internal int BaseLength { get; }
    internal ushort StreamVersion { get; }
    internal int StrippedFolderCount { get; }
    internal int MarkerOffset { get; }
    internal uint MarkerValue { get; }
    internal int DepthSlot1 { get; }
    internal int DepthSlot2 { get; }
    internal double MeasuredDepth { get; }
    internal byte[] Block { get; }
}

internal static class SurfaceExtrudeAuthoringTemplates
{
    internal static readonly SurfaceExtrudeAuthoringTemplate SurfaceExtrudePrimary = new SurfaceExtrudeAuthoringTemplate(
        "Primary", "SurfaceExtrude", "moExtruRefSurface_c",
        "a0578fe6e50578522109d4d36c4482e0642d8d95e45fc6c514df87e518b939da", 8928, 0x70, 19,
        8912, 5u, 2542, 2614, 0.01,
        ExtrudeAuthoringSegments.SurfaceExtrudeSource);

    internal static readonly SurfaceExtrudeAuthoringTemplate SurfaceExtrudeResave = new SurfaceExtrudeAuthoringTemplate(
        "Resave", "SurfaceExtrude", "moExtruRefSurface_c",
        "1c2b0f38eb0d55582db68901bb6fc31f179ff0d81beb5ff2a06ca44ed431aff8", 8928, 0x70, 19,
        8912, 5u, 2820, 2892, 0.01,
        ExtrudeAuthoringSegments.SurfaceExtrudeResave);

    internal static readonly SurfaceExtrudeAuthoringTemplate[] All =
    {
        SurfaceExtrudePrimary,
        SurfaceExtrudeResave,
    };
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SurfaceExtrudeFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured surface-extrude authoring from the controlled surf_extrude_10mm sample.
/// The target must be the exact stripped base (stream 0x70, folder count 19, 0x67 marker
/// at the measured position with value 0, normalized SHA). Only the depth is parameterized,
/// patched into the two validated slots. All other fields remain measured defaults.
/// </summary>
internal static class SurfaceExtrudeFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream, double depth, string? featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (double.IsNaN(depth) || double.IsInfinity(depth) || depth <= 0 || depth > 1.0)
            throw new ArgumentOutOfRangeException(nameof(depth),
                $"Surface extrude depth must be a positive finite value <= 1.0 m, but received {depth}.");

        var template = SelectTemplate(currentStream);
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Surface extrude measured authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, SurfaceExtrudeFeatureParser.ClassName) > 0)
        {
            throw new NotSupportedException(
                "The document already contains a moExtruRefSurface_c feature. No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        foreach (int slot in new[] { template.DepthSlot1, template.DepthSlot2 })
        {
            if (slot + 8 > block.Length)
                throw new NotSupportedException("Surface extrude template block is truncated. No stream changes have been made.");
            double measured = BitConverter.ToDouble(block, slot);
            if (Math.Abs(measured - template.MeasuredDepth) > 1e-15)
                throw new NotSupportedException(
                    $"Surface extrude depth slot 0x{slot:X} changed; refusing to patch an unknown layout. " +
                    "No stream changes have been made.");
            BitConverter.GetBytes(depth).CopyTo(block, slot);
        }

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, template.MarkerOffset + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return (newStream, template.Name);
    }

    private static SurfaceExtrudeAuthoringTemplate SelectTemplate(byte[] currentStream)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in SurfaceExtrudeAuthoringTemplates.All)
        {
            if (currentStream.Length != template.BaseLength)
                continue;
            if (version != template.StreamVersion)
                continue;
            if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
                continue;
            if (template.MarkerOffset + 8 > currentStream.Length)
                continue;
            if (currentStream[template.MarkerOffset] != 0x67 ||
                currentStream[template.MarkerOffset + 1] != 0 ||
                currentStream[template.MarkerOffset + 2] != 0 ||
                currentStream[template.MarkerOffset + 3] != 0 ||
                BitConverter.ToUInt32(currentStream, template.MarkerOffset + 4) != 0)
                continue;

            byte[] normalized = (byte[])currentStream.Clone();
            BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
            BitConverter.GetBytes(0u).CopyTo(normalized, template.MarkerOffset + 4);
            string sha;
            using (var hash = SHA256.Create())
                sha = BitConverter.ToString(hash.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
            if (string.Equals(sha, template.BaseSha256, StringComparison.Ordinal))
                return template;
        }

        throw new NotSupportedException(
            $"The document is not the controlled stripped surface-extrude base " +
            $"(stream length {currentStream.Length}, version 0x{version:X2}). No stream changes have been made.");
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SurfaceExtrudeFeatureParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict parser for the measured 2026 surface-extrude layout
/// (moExtruRefSurface_c, stream 0x70). Only the depth (two identical slots after
/// ParallelPlaneDistanceDim_c) and the folder-header type code are decoded.
/// </summary>
internal static class SurfaceExtrudeFeatureParser
{
    internal const string ClassName = "moExtruRefSurface_c";
    internal const uint ExpectedTypeId = 0x4F;
    internal const ushort ExpectedStreamVersion = 0x70;

    internal const int DepthSlot1FromPlaneDim = 0x5D;
    internal const int DepthSlot2FromPlaneDim = 0xA5;

    private static readonly int[] DepthSlotsFromPlaneDim = { DepthSlot1FromPlaneDim, DepthSlot2FromPlaneDim };

    public static SurfaceExtrudeFeatureModel? Parse(MfcElement element)
    {
        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: ClassName,
            minTypeId: ExpectedTypeId,
            maxTypeId: ExpectedTypeId,
            expectedStreamVersion: ExpectedStreamVersion);
        if (ctx == null)
            return null;

        var planeDim = ctx.Nodes.FirstOrDefault(t => t.ClassName == "ParallelPlaneDistanceDim_c" && t.TagOffset > ctx.Start);
        if (planeDim == null)
            return null;

        if (!ctx.Parameters.TryReadVerifiedDoubleSlots(
                planeDim.TagOffset,
                DepthSlotsFromPlaneDim,
                out double depth,
                minExclusive: 0.0,
                maxInclusive: 1.0,
                tolerance: 1e-15))
        {
            return null;
        }

        var definition = new SurfaceExtrudeFeatureModel
        {
            SerializedClassName = ClassName,
            SerializedTypeId = ctx.TypeId,
            Depth = depth,
            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(ctx)
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Depth = true;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        return definition;
    }
}


