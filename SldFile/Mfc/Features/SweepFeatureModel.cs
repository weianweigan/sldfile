using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Direct strongly-typed MFC model for moSweep_c (Boss Sweep) and moSweepCut_c (Cut Sweep).
/// Implements IMfcFeatureModel and IMfcSerializable for 100% symmetric reading and writing.
/// </summary>
public sealed class SweepFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string BossClassName = "moSweep_c";
    public const string CutClassName = "moSweepCut_c";
    public const string SurfaceClassName = "moSweepRefSurface_c";

    public const uint CircularTypeId = 0x4B;
    public const uint ProfilePathTypeId = 0x4C;
    public const uint GuidesTypeId = 0x4D;

    public string ClassName => IsCut ? CutClassName : IsSurface ? SurfaceClassName : BossClassName;
    public string FeatureName { get; set; } = "扫描1";
    public string DisplayName => FeatureName;
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? CalculateEffectiveTypeId();
        set => SerializedTypeId = value;
    }

    public bool IsCut { get; set; } = false;
    public bool IsSurface { get; set; } = false;

    // Topology Curve References
    public MfcSketchCurveReference? ProfileReference { get; set; }
    public MfcSketchCurveReference? PathReference { get; set; }
    public List<MfcSketchCurveReference> GuideReferences { get; } = new();

    private IReadOnlyList<ushort> _selectionReferenceIds = Array.Empty<ushort>();
    public IReadOnlyList<ushort> SelectionReferenceIds
    {
        get => _selectionReferenceIds;
        set => _selectionReferenceIds = value ?? Array.Empty<ushort>();
    }

    public int SelectionCount => SelectionReferenceIds.Count;

    private IReadOnlyList<SketchCurveReference> _references = Array.Empty<SketchCurveReference>();
    public IReadOnlyList<SketchCurveReference> References
    {
        get
        {
            if (_references != null && _references.Count > 0)
                return _references;
            var list = new List<SketchCurveReference>();
            if (CircularProfile)
            {
                if (PathReference != null) list.Add(PathReference);
            }
            else
            {
                if (ProfileReference != null) list.Add(ProfileReference);
                if (PathReference != null) list.Add(PathReference);
            }
            list.AddRange(GuideReferences);
            return list.AsReadOnly();
        }
        set => _references = value ?? Array.Empty<SketchCurveReference>();
    }

    public bool IsHeaderSelectionIdMappingPositional { get; set; } = true;

    // Circular Profile
    public bool CircularProfile { get; set; } = false;
    public bool CircularProfileLayout => CircularProfile;
    public double? CircularProfileDiameter { get; set; }

    // Thin Feature
    public bool IsThinFeature { get; set; } = false;
    public bool ThinFeatureLayout => IsThinFeature;
    public uint ThinWallType { get; set; } = 0;
    public double? WallThickness { get; set; }
    public double? ThinWallThickness => WallThickness;
    public double WallThicknessDirection2 { get; set; } = 0.0;

    // Sweep Options
    public bool? Merge { get; set; } = true;
    public int? TwistControlType { get; set; } = 0;
    public double? TwistAngleRad { get; set; } = 0.0;
    public int? PathAlignmentType { get; set; } = 0;
    public bool MaintainTangency { get; set; } = true;
    public bool AdvancedSmoothing { get; set; } = false;
    public bool AlignWithEndFaces { get; set; } = false;

    // Multi-body Scope
    public bool AutoSelect { get; set; } = true;
    public bool FeatureScope { get; set; } = false;
    public List<uint> FeatureScopeBodyIds { get; } = new();

    public SweepFeatureParameterResolution Resolution { get; } = new();

    public byte[]? RawPayload { get; set; }

    public SweepFeatureModel()
    {
    }

    public SweepFeatureModel(
        string displayName,
        uint serializedTypeId,
        IReadOnlyList<ushort> selectionReferenceIds,
        IReadOnlyList<SketchCurveReference> references,
        bool circularProfileLayout,
        double? circularProfileDiameter,
        bool thinFeatureLayout,
        double? thinWallThickness,
        bool isHeaderSelectionIdMappingPositional,
        bool? merge = null,
        int? twistControlType = null,
        double? twistAngleRad = null,
        int? pathAlignmentType = null)
    {
        FeatureName = displayName ?? string.Empty;
        SerializedTypeId = serializedTypeId;
        SelectionReferenceIds = selectionReferenceIds ?? Array.Empty<ushort>();
        References = references ?? Array.Empty<SketchCurveReference>();
        CircularProfile = circularProfileLayout;
        CircularProfileDiameter = circularProfileDiameter;
        IsThinFeature = thinFeatureLayout;
        WallThickness = thinWallThickness;
        IsHeaderSelectionIdMappingPositional = isHeaderSelectionIdMappingPositional;
        Merge = merge;
        TwistControlType = twistControlType;
        TwistAngleRad = twistAngleRad;
        PathAlignmentType = pathAlignmentType;

        bool hasProfileOrPath = false;
        bool hasGuide = false;
        foreach (var reference in References)
        {
            if (reference.Role == SketchCurveRole.Profile)
            {
                ProfileReference = reference as MfcSketchCurveReference ?? new MfcSketchCurveReference(reference.EntityId, reference.Role, reference.HeaderSelectionId);
                hasProfileOrPath = true;
            }
            else if (reference.Role == SketchCurveRole.Path)
            {
                PathReference = reference as MfcSketchCurveReference ?? new MfcSketchCurveReference(reference.EntityId, reference.Role, reference.HeaderSelectionId);
                hasProfileOrPath = true;
            }
            else if (reference.Role == SketchCurveRole.Guide)
            {
                GuideReferences.Add(reference as MfcSketchCurveReference ?? new MfcSketchCurveReference(reference.EntityId, reference.Role, reference.HeaderSelectionId));
                hasGuide = true;
            }
        }

        Resolution.DisplayName = !string.IsNullOrEmpty(FeatureName);
        Resolution.SerializedTypeId = true;
        Resolution.SelectionReferenceList = SelectionReferenceIds.Count > 0;
        Resolution.ProfileAndPathReferences = hasProfileOrPath;
        Resolution.GuideCurveReferences = hasGuide;
        Resolution.ThinFeature = ThinFeatureLayout;
        Resolution.CircularProfile = CircularProfileLayout;
        Resolution.Merge = Merge.HasValue;
        Resolution.PathAlignmentType = PathAlignmentType.HasValue;
        Resolution.TwistControlType = TwistControlType.HasValue;
        Resolution.TwistAngle = TwistAngleRad.HasValue;
        Resolution.MergeAndScope = false;
        Resolution.SweepParameters = false;
    }

    public uint CalculateEffectiveTypeId()
    {
        if (CircularProfile) return CircularTypeId;
        if (GuideReferences.Count > 0) return GuidesTypeId;
        return ProfilePathTypeId;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            // 1. Tag & ClassName
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            // 2. Schema
            ushort schema = 0x8004;
            ar.U16(ref schema);

            // 3. Feature Name (WChar)
            string? name = FeatureName;
            ar.WChar(ref name);

            // 4. Version (2.0)
            double ver = Version;
            ar.F64(ref ver);

            // 5. TypeId & Zero
            uint tid = TypeId;
            ar.U32(ref tid);
            uint zero = 0;
            ar.U32(ref zero);

            // 6. Empty WChar String & Padding
            uint emptyPrefix = 0x00FFFEFF;
            ar.U32(ref emptyPrefix);
            ushort pad1 = 0; ar.U16(ref pad1);
            uint pad2 = 0; ar.U32(ref pad2);

            // 7. Selection Reference Count & IDs
            var selIds = CollectSelectionIds();
            ushort selCount = (ushort)selIds.Count;
            ar.U16(ref selCount);
            foreach (var id in selIds)
            {
                ushort idCopy = id;
                ar.U16(ref idCopy);
            }

            // 8. Fixed Header Suffix (12 zeros + 0xA3 + 5 zeros)
            SerializeHeaderSuffix(ar);

            // 9. Profile Wrapper (moCompProfile_c, stride 188)
            SerializeCompProfile(ar);

            // 10. Secondary Curve Wrapper (moWireContact_c, stride 190) & Option Tail
            SerializeWireContact(ar);

            // 11. Circular Profile Dimension (if applicable)
            if (CircularProfile || (CircularProfileDiameter.HasValue && CircularProfileDiameter.Value > 0.0))
            {
                SerializeCircularDimension(ar);
            }

            // 12. Thin Wall Feature Dimension (if applicable)
            if (IsThinFeature || (WallThickness.HasValue && WallThickness.Value > 0.0))
            {
                SerializeThinWall(ar);
            }
        }
        else
        {
            // Deserialization mirrors exact reading sequence
            ushort refTag = 0;
            var kind = ar.ReadClassTag(out string cls, out refTag, out _);
            if (!string.IsNullOrEmpty(cls))
            {
                if (cls == CutClassName) IsCut = true;
                else if (cls == SurfaceClassName) IsSurface = true;
                Resolution.DisplayName = true;
            }

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            if (!string.IsNullOrEmpty(name))
            {
                FeatureName = name!;
                Resolution.DisplayName = true;
            }

            double ver = 0;
            ar.F64(ref ver);
            Version = ver;

            uint tid = 0;
            ar.U32(ref tid);
            SerializedTypeId = tid;
            Resolution.SerializedTypeId = true;
            if (tid == CircularTypeId)
            {
                CircularProfile = true;
                Resolution.CircularProfile = true;
            }

            uint zero = 0;
            ar.U32(ref zero);

            uint emptyPrefix = 0;
            ar.U32(ref emptyPrefix);
            ushort pad1 = 0; ar.U16(ref pad1);
            uint pad2 = 0; ar.U32(ref pad2);

            ushort selCount = 0;
            ar.U16(ref selCount);
            var selIds = new List<ushort>();
            for (int i = 0; i < selCount; i++)
            {
                ushort id = 0;
                ar.U16(ref id);
                selIds.Add(id);
            }
            _selectionReferenceIds = selIds.AsReadOnly();
            Resolution.SelectionReferenceList = selIds.Count > 0;

            DeserializeHeaderSuffix(ar);

            if (ar.Remaining > 0)
            {
                DeserializeBody(ar);
            }
        }
    }

    private List<ushort> CollectSelectionIds()
    {
        var ids = new List<ushort>();
        if (SelectionReferenceIds.Count > 0)
        {
            ids.AddRange(SelectionReferenceIds);
            return ids;
        }
        ushort baseId = 0xC2;
        if (CircularProfile)
        {
            ids.Add(PathReference?.HeaderSelectionId > 0 ? PathReference.HeaderSelectionId : (ushort)0xBF);
        }
        else
        {
            ids.Add(ProfileReference?.HeaderSelectionId > 0 ? ProfileReference.HeaderSelectionId : baseId++);
            if (PathReference != null)
                ids.Add(PathReference.HeaderSelectionId > 0 ? PathReference.HeaderSelectionId : baseId++);
            foreach (var g in GuideReferences)
                ids.Add(g.HeaderSelectionId > 0 ? g.HeaderSelectionId : baseId++);
        }
        return ids;
    }

    private void SerializeHeaderSuffix(MfcArchive ar)
    {
        for (int i = 0; i < 3; i++) { uint z = 0; ar.U32(ref z); }
        uint marker = 0xA3; ar.U32(ref marker);
        uint z2 = 0; ar.U32(ref z2);
        byte b0 = 0; ar.Byte(ref b0);
    }

    private void DeserializeHeaderSuffix(MfcArchive ar)
    {
        for (int i = 0; i < 3; i++) { uint z = 0; ar.U32(ref z); }
        uint marker = 0; ar.U32(ref marker);
        uint z2 = 0; ar.U32(ref z2);
        byte b0 = 0; ar.Byte(ref b0);
    }

    private void SerializeCompProfile(MfcArchive ar)
    {
        ushort tag = 0;
        ar.ClassTagOrRef("moCompProfile_c", ref tag);
        ushort schema = 0x8004;
        ar.U16(ref schema);

        var primaryRef = CircularProfile ? PathReference : ProfileReference;
        if (primaryRef != null)
        {
            primaryRef.Serialize(ar);
        }
        else
        {
            new MfcSketchCurveReference(0x49, CircularProfile ? SketchCurveRole.Path : SketchCurveRole.Profile).Serialize(ar);
        }

        MfcSketchCurveReference.WriteNullPlaceholder(ar);
        byte[] meta = new byte[84];
        ar.WriteBytes(meta);
    }

    private void SerializeWireContact(MfcArchive ar)
    {
        ushort tag = 0;
        ar.ClassTagOrRef("moWireContact_c", ref tag);
        ushort schema = 0x8004;
        ar.U16(ref schema);

        if (!CircularProfile && PathReference != null)
        {
            PathReference.Serialize(ar);
            MfcSketchCurveReference.WriteNullPlaceholder(ar);
            byte[] meta = new byte[86];
            ar.WriteBytes(meta);
        }

        foreach (var guide in GuideReferences)
        {
            guide.Serialize(ar);
            MfcSketchCurveReference.WriteNullPlaceholder(ar);
            byte[] meta = new byte[86];
            ar.WriteBytes(meta);
        }

        byte twist = (byte)(TwistControlType ?? 0);
        ar.Byte(ref twist);

        byte m = (Merge ?? true) ? (byte)1 : (byte)0;
        ar.Byte(ref m);

        double angle = TwistAngleRad ?? 0.0;
        ar.F64(ref angle);
    }

    private void SerializeCircularDimension(MfcArchive ar)
    {
        ushort dimTag = 0;
        ar.ClassTagOrRef("ThreeDdiameterDim_c", ref dimTag);
        ushort dimSchema = 0x8004; ar.U16(ref dimSchema);

        ushort paramTag = 0;
        ar.ClassTagOrRef("moLengthParameter_c", ref paramTag);
        ushort paramSchema = 0x8004; ar.U16(ref paramSchema);

        byte[] paramPrefix = new byte[30];
        BitConverter.GetBytes(0x00FFFEFFu).CopyTo(paramPrefix, 0);
        paramPrefix[4] = 2; paramPrefix[5] = 0;
        paramPrefix[6] = (byte)'D'; paramPrefix[7] = 0;
        paramPrefix[8] = (byte)'3'; paramPrefix[9] = 0;
        paramPrefix[10] = 0; paramPrefix[11] = 0;
        ar.WriteBytes(paramPrefix);

        double diam = CircularProfileDiameter ?? 0.0;
        ar.F64(ref diam);
    }

    private void SerializeThinWall(MfcArchive ar)
    {
        ushort dimTag = 0;
        ar.ClassTagOrRef("ParallelPlaneDistanceDim_c", ref dimTag);
        ushort dimSchema = 0x8004; ar.U16(ref dimSchema);

        ushort paramTag = 0;
        ar.ClassTagOrRef("moLengthParameter_c", ref paramTag);
        ushort paramSchema = 0x8004; ar.U16(ref paramSchema);

        byte[] paramPrefix = new byte[30];
        BitConverter.GetBytes(0x00FFFEFFu).CopyTo(paramPrefix, 0);
        paramPrefix[4] = 2; paramPrefix[5] = 0;
        paramPrefix[6] = (byte)'D'; paramPrefix[7] = 0;
        paramPrefix[8] = (byte)'1'; paramPrefix[9] = 0;
        paramPrefix[10] = 0; paramPrefix[11] = 0;
        ar.WriteBytes(paramPrefix);

        double th1 = WallThickness ?? 0.0;
        ar.F64(ref th1);

        uint type = ThinWallType;
        ar.U32(ref type);
        double th2 = WallThicknessDirection2;
        ar.F64(ref th2);
    }

    private void DeserializeBody(MfcArchive ar)
    {
        byte[] rem = ar.ReadRemainingBytes();
        if (rem.Length == 0) return;

        // Check if moCompProfile_c is present
        int profileTag = MfcPrimitives.FindAscii(rem, "moCompProfile_c");
        if (profileTag >= 0)
        {
            int dataStart = profileTag + "moCompProfile_c".Length + 2; // skip schema
            if (dataStart + 52 <= rem.Length)
            {
                uint magic = BitConverter.ToUInt32(rem, dataStart);
                if (magic == MfcSketchCurveReference.RefMagic)
                {
                    uint entId = BitConverter.ToUInt32(rem, dataStart + 4);
                    var curveRef = new MfcSketchCurveReference(
                        entId,
                        CircularProfile ? SketchCurveRole.Path : SketchCurveRole.Profile,
                        SelectionReferenceIds.Count > 0 ? SelectionReferenceIds[0] : (ushort)0);
                    if (CircularProfile)
                        PathReference = curveRef;
                    else
                        ProfileReference = curveRef;
                    Resolution.ProfileAndPathReferences = true;
                }
            }
        }

        // Check if moWireContact_c is present
        int wireTag = MfcPrimitives.FindAscii(rem, "moWireContact_c");
        if (wireTag >= 0)
        {
            int currentOffset = wireTag + "moWireContact_c".Length + 2; // skip schema

            // 1. PathReference (if not circular profile)
            if (!CircularProfile && currentOffset + 52 <= rem.Length)
            {
                uint magic = BitConverter.ToUInt32(rem, currentOffset);
                if (magic == MfcSketchCurveReference.RefMagic)
                {
                    uint entId = BitConverter.ToUInt32(rem, currentOffset + 4);
                    PathReference = new MfcSketchCurveReference(
                        entId,
                        SketchCurveRole.Path,
                        SelectionReferenceIds.Count > 1 ? SelectionReferenceIds[1] : (ushort)0);
                    Resolution.ProfileAndPathReferences = true;
                    currentOffset += 190;
                }
            }

            // 2. GuideReferences loop (stride 190 per curve)
            GuideReferences.Clear();
            int guideIndex = 0;
            while (currentOffset + 190 <= rem.Length)
            {
                uint magic = BitConverter.ToUInt32(rem, currentOffset);
                if (magic != MfcSketchCurveReference.RefMagic)
                    break;

                uint sentinel = BitConverter.ToUInt32(rem, currentOffset + 36);
                if (sentinel != MfcSketchCurveReference.RefSentinel)
                    break;

                uint entId = BitConverter.ToUInt32(rem, currentOffset + 4);
                int selIdx = (CircularProfile ? 1 : 2) + guideIndex;
                ushort headerSelId = selIdx < SelectionReferenceIds.Count ? SelectionReferenceIds[selIdx] : (ushort)0;
                var guideRef = new MfcSketchCurveReference(entId, SketchCurveRole.Guide, headerSelId);
                GuideReferences.Add(guideRef);
                guideIndex++;
                currentOffset += 190;
            }
            if (GuideReferences.Count > 0)
            {
                Resolution.GuideCurveReferences = true;
            }

            // 3. Option tail dynamically aligned at currentOffset
            if (currentOffset + 10 <= rem.Length)
            {
                byte twist = rem[currentOffset];
                byte m = rem[currentOffset + 1];
                double ang = BitConverter.ToDouble(rem, currentOffset + 2);

                TwistControlType = twist;
                Merge = m == 1;
                TwistAngleRad = ang;
                Resolution.TwistControlType = true;
                Resolution.Merge = true;
                Resolution.TwistAngle = true;
            }
        }

        // Check if ThreeDdiameterDim_c is present
        int diamTag = MfcPrimitives.FindAscii(rem, "ThreeDdiameterDim_c");
        if (diamTag >= 0)
        {
            CircularProfile = true;
            Resolution.CircularProfile = true;
            int paramTag = MfcPrimitives.FindAscii(rem, "moLengthParameter_c", diamTag);
            if (paramTag >= 0)
            {
                int valOffset = paramTag + "moLengthParameter_c".Length + 32;
                if (valOffset + 8 <= rem.Length)
                {
                    double d = BitConverter.ToDouble(rem, valOffset);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && d >= 0.0)
                    {
                        CircularProfileDiameter = d;
                    }
                }
            }
        }

        // Check if ParallelPlaneDistanceDim_c is present
        int thinTag = MfcPrimitives.FindAscii(rem, "ParallelPlaneDistanceDim_c");
        if (thinTag >= 0)
        {
            IsThinFeature = true;
            Resolution.ThinFeature = true;
            int paramTag = MfcPrimitives.FindAscii(rem, "moLengthParameter_c", thinTag);
            int valOffset;
            if (paramTag >= 0)
            {
                valOffset = paramTag + "moLengthParameter_c".Length + 32;
            }
            else
            {
                // Class tag reference (2 bytes) instead of new class declaration
                valOffset = thinTag + "ParallelPlaneDistanceDim_c".Length + 36;
            }

            if (valOffset + 8 <= rem.Length)
            {
                double th = BitConverter.ToDouble(rem, valOffset);
                if (!double.IsNaN(th) && !double.IsInfinity(th) && th >= 0.0)
                {
                    WallThickness = th;
                }
            }
            int extraOffset = valOffset + 8;
            if (extraOffset + 12 <= rem.Length)
            {
                ThinWallType = BitConverter.ToUInt32(rem, extraOffset);
                WallThicknessDirection2 = BitConverter.ToDouble(rem, extraOffset + 4);
            }
        }

        RawPayload = rem;
    }

    internal static SweepFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        if (element.ClassName != BossClassName && element.ClassName != CutClassName && element.ClassName != SurfaceClassName)
            return null;

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            int length = block.Length - offset;
            byte[] rawSlice = new byte[length];
            Buffer.BlockCopy(block, offset, rawSlice, 0, length);
            var ar = MfcArchive.CreateReader(rawSlice);
            var model = new SweepFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.DisplayName && model.Version == 2.0 && model.SerializedTypeId.HasValue)
                return model;
        }
        catch { }

        var selection = SweepLoftSelectionDecoder.Decode(element);
        if (selection != null && selection.Kind == SweepLoftSelectionKind.Sweep)
        {
            var options = ReadSweepOptions(element);

            return new SweepFeatureModel(
                element.AsWChar() ?? string.Empty,
                selection.SerializedTypeCode,
                selection.HeaderSelectionIds,
                selection.References,
                selection.CircularProfileLayout,
                selection.CircularProfileDiameter,
                selection.ThinFeatureLayout,
                selection.ThinWallThickness,
                selection.IsHeaderSelectionIdMappingPositional,
                options.Merge,
                options.TwistControlType,
                options.TwistAngleRad);
        }

        return null;
    }

    private static (bool? Merge, int? TwistControlType, double? TwistAngleRad) ReadSweepOptions(MfcElement element)
    {
        byte[] data = element.SourceData;
        int end = element.DataEnd;
        if (end < 0 || end + 0x157 > data.Length)
            return (null, null, null);

        byte mergeByte = data[end + 0x147];
        if (mergeByte > 1)
            return (null, null, null);
        byte twistByte = data[end + 0x113];
        if (twistByte != 0 && twistByte != 1 && twistByte != 8)
            return (null, null, null);
        double angle = BitConverter.ToDouble(data, end + 0x14F);
        if (double.IsNaN(angle) || double.IsInfinity(angle) || angle < 0 || angle > Math.PI * 2 + 1e-9)
            return (null, null, null);
        return (mergeByte == 1, (int)twistByte, angle);
    }

    private sealed class SweepFamily
    {
        public ushort StreamVersion { get; set; }
        public uint TypeId { get; set; }
        public int WireContactDelta { get; set; }
        public int MergeOffset { get; set; }
        public int PathAlignmentOffset { get; set; }
        public int TwistAngleOffset { get; set; }
    }

    private static readonly SweepFamily[] SweepFamilies =
    {
        new() { StreamVersion = 0x0CFF, TypeId = 0x123, WireContactDelta = 0x68F, MergeOffset = 0xCC2, PathAlignmentOffset = 0xCC7, TwistAngleOffset = 0xCCB },
        new() { StreamVersion = 0x0073, TypeId = 0x2E, WireContactDelta = 0x6B8, MergeOffset = 0x161, PathAlignmentOffset = 0x17E, TwistAngleOffset = 0x169 },
        new() { StreamVersion = 0x007B, TypeId = 0xCF, WireContactDelta = 0xABF, MergeOffset = 0x147, PathAlignmentOffset = 0x162, TwistAngleOffset = 0x157 },
        new() { StreamVersion = 0x0069, TypeId = 0x159, WireContactDelta = 0x8AA, MergeOffset = 0x147, PathAlignmentOffset = 0x164, TwistAngleOffset = 0x14F }
    };

    internal static IFeatureData? ParseLegacySweep(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        var ctx = FeatureWindowContext.TryCreateWithFolderHeader(
            doc, className, typeId, start, doc?.SourceData.Length ?? end, out var header, requiredClassName: BossClassName);
        if (ctx == null || ctx.RootElement == null)
            return null;

        var family = SweepFamilies.FirstOrDefault(f =>
            f.StreamVersion == ctx.StreamVersion && f.TypeId == header.TypeId);
        if (family == null)
            return null;

        var wire = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moWireContact_c" && t.TagOffset > start);
        if (wire == null || wire.TagOffset - start != family.WireContactDelta)
            return null;

        byte[] data = ctx.RawData;
        int anchor = wire.TagOffset;
        bool? merge = null;
        int? pathAlignment = null;
        double? twistAngle = null;
        int? twistControlType = null;

        int mergeOffset = anchor + family.MergeOffset;
        if (mergeOffset >= 0 && mergeOffset < data.Length && data[mergeOffset] <= 1)
            merge = data[mergeOffset] != 0;

        int pathOffset = anchor + family.PathAlignmentOffset;
        if (pathOffset >= 0 && pathOffset < data.Length)
        {
            pathAlignment = data[pathOffset] switch
            {
                0 => 0,
                1 => 10,
                _ => (int?)null
            };
        }

        if (ctx.Parameters.TryReadVerifiedDoubleSlots(
                anchor,
                new[] { family.TwistAngleOffset },
                out double angle,
                minExclusive: -Math.PI * 2 - 1e-9,
                maxInclusive: Math.PI * 2 + 1e-9))
        {
            twistAngle = angle;
        }

        long markerLimit = (long)anchor + 0x400;
        if (ctx.Nodes.Any(t => t.ClassName == "moTwistDataForBiDirectionalSweep_c" &&
                               t.TagOffset > start && t.TagOffset < markerLimit))
        {
            twistControlType = 8;
        }

        return new SweepFeatureModel(
            ctx.RootElement.AsWChar() ?? string.Empty,
            header.TypeId,
            Array.Empty<ushort>(),
            Array.Empty<SketchCurveReference>(),
            false,
            null,
            false,
            null,
            false,
            merge,
            twistControlType,
            twistAngle,
            pathAlignment);
    }
}
