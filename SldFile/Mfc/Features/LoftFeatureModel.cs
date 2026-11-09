using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Direct strongly-typed MFC model for moBlend_c (Boss Loft) and moBlendCut_c (Cut Loft).
/// Implements IMfcFeatureModel and IMfcSerializable for 100% symmetric reading and writing.
/// </summary>
public sealed class LoftFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string BossClassName = "moBlend_c";
    public const string CutClassName = "moBlendCut_c";
    public const string SurfaceClassName = "moBlendRefSurface_c";

    public const uint TwoProfilesTypeId = 0x4D;
    public const uint GuidesOrCenterlineTypeId = 0x4E;
    public const uint ThreeProfilesTypeId = 0x50;

    public string ClassName => IsCut ? CutClassName : IsSurface ? SurfaceClassName : BossClassName;
    public string FeatureName { get; set; } = "放样1";
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

    // Topology Profiles & Guides
    public List<MfcSketchCurveReference> ProfileReferences { get; } = new();
    public List<MfcSketchCurveReference> GuideReferences { get; } = new();
    private SketchCurveReference? _centerline;
    public MfcSketchCurveReference? CenterlineReference { get; set; }
    public SketchCurveReference? Centerline
    {
        get => _centerline ?? CenterlineReference;
        set
        {
            _centerline = value;
            CenterlineReference = value as MfcSketchCurveReference ?? (value != null ? new MfcSketchCurveReference(value.EntityId, value.Role, value.HeaderSelectionId) : null);
        }
    }

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
            list.AddRange(ProfileReferences);
            list.AddRange(GuideReferences);
            if (CenterlineReference != null) list.Add(CenterlineReference);
            return list.AsReadOnly();
        }
        set => _references = value ?? Array.Empty<SketchCurveReference>();
    }

    public bool IsHeaderSelectionIdMappingPositional { get; set; } = true;

    // Boundary Tangency Conditions
    public int? StartTangencyType { get; set; }
    public int? EndTangencyType { get; set; }
    public double StartTangentLength { get; set; } = 1.0;
    public double EndTangentLength { get; set; } = 1.0;
    public double StartDraftAngle { get; set; } = 0.0;
    public double EndDraftAngle { get; set; } = 0.0;

    // Loft Options
    public bool Close { get; set; } = false;
    public bool? Merge { get; set; } = true;
    public bool MaintainTangency { get; set; } = true;
    public int GuideCurveInfluence { get; set; } = 0;

    // Thin Feature
    public bool IsThinFeature { get; set; } = false;
    public bool ThinFeatureLayout => IsThinFeature;
    public uint ThinWallType { get; set; } = 0;
    public double? WallThickness { get; set; }
    public double? ThinWallThickness => WallThickness;
    public double WallThicknessDirection2 { get; set; } = 0.0;

    public bool CircularProfileLayout => false;
    public double? CircularProfileDiameter => null;

    public LoftFeatureParameterResolution Resolution { get; } = new();

    public byte[]? RawPayload { get; set; }

    public LoftFeatureModel()
    {
    }

    public LoftFeatureModel(
        string displayName,
        uint serializedTypeId,
        IReadOnlyList<ushort> selectionReferenceIds,
        IReadOnlyList<SketchCurveReference> references,
        bool circularProfileLayout,
        double? circularProfileDiameter,
        bool thinFeatureLayout,
        double? thinWallThickness,
        bool isHeaderSelectionIdMappingPositional,
        int? startTangencyType = null,
        int? endTangencyType = null,
        SketchCurveReference? centerline = null)
    {
        FeatureName = displayName ?? string.Empty;
        SerializedTypeId = serializedTypeId;
        SelectionReferenceIds = selectionReferenceIds ?? Array.Empty<ushort>();
        References = references ?? Array.Empty<SketchCurveReference>();
        IsThinFeature = thinFeatureLayout;
        WallThickness = thinWallThickness;
        IsHeaderSelectionIdMappingPositional = isHeaderSelectionIdMappingPositional;
        StartTangencyType = startTangencyType;
        EndTangencyType = endTangencyType;
        Centerline = centerline;

        bool hasProfile = false;
        bool hasGuide = false;
        foreach (var reference in References)
        {
            if (reference.Role == SketchCurveRole.Profile)
            {
                ProfileReferences.Add(reference as MfcSketchCurveReference ?? new MfcSketchCurveReference(reference.EntityId, reference.Role, reference.HeaderSelectionId));
                hasProfile = true;
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
        Resolution.Profiles = hasProfile;
        Resolution.GuideCurves = hasGuide;
        Resolution.Centerline = Centerline != null;
        Resolution.StartEndConstraints = false;
        Resolution.StartTangencyType = StartTangencyType.HasValue;
        Resolution.EndTangencyType = EndTangencyType.HasValue;
        Resolution.ThinFeature = ThinFeatureLayout;
        Resolution.CircularProfile = false;
        Resolution.MergeAndScope = false;
        Resolution.LoftParameters = false;
    }

    public uint CalculateEffectiveTypeId()
    {
        if (CenterlineReference != null || GuideReferences.Count > 0) return GuidesOrCenterlineTypeId;
        if (ProfileReferences.Count >= 3) return ThreeProfilesTypeId;
        return TwoProfilesTypeId;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            // 1. Tag & ClassName ("moBlend_c")
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            // 2. Schema (0x8004)
            ushort schema = 0x8004;
            ar.U16(ref schema);

            // 3. Feature Name
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

            // 6. Empty WChar Prefix & Padding
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

            // 8. Header Suffix
            SerializeHeaderSuffix(ar);

            // 9. Profile Wrapper (moCompProfile_c, stride 188 per profile)
            SerializeProfiles(ar);

            // 10. Secondary Curves Wrapper (moWireContact_c, stride 190)
            if (GuideReferences.Count > 0 || CenterlineReference != null)
            {
                SerializeSecondaryCurves(ar);
            }

            // 11. Tangency Condition Tail
            SerializeTangencyTail(ar);

            // 12. Thin Wall Feature Dimension (if applicable)
            if (IsThinFeature || (WallThickness.HasValue && WallThickness.Value > 0.0))
            {
                SerializeThinWall(ar);
            }
        }
        else
        {
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
        if (SelectionReferenceIds.Count > 0) return SelectionReferenceIds.ToList();
        ushort baseId = 0xC5;
        foreach (var p in ProfileReferences)
            ids.Add(p.HeaderSelectionId > 0 ? p.HeaderSelectionId : baseId++);
        foreach (var g in GuideReferences)
            ids.Add(g.HeaderSelectionId > 0 ? g.HeaderSelectionId : baseId++);
        if (CenterlineReference != null)
            ids.Add(CenterlineReference.HeaderSelectionId > 0 ? CenterlineReference.HeaderSelectionId : baseId++);
        return ids;
    }

    private void SerializeHeaderSuffix(MfcArchive ar)
    {
        for (int i = 0; i < 3; i++) { uint z = 0; ar.U32(ref z); }
        uint marker = (StartTangencyType > 0 || EndTangencyType > 0) ? 0xA8u : 0xA6u;
        ar.U32(ref marker);
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

    private void SerializeProfiles(MfcArchive ar)
    {
        ushort tag = 0;
        ar.ClassTagOrRef("moCompProfile_c", ref tag);
        ushort schema = 0x8004;
        ar.U16(ref schema);

        var profiles = ProfileReferences.Count > 0
            ? ProfileReferences
            : new List<MfcSketchCurveReference>
            {
                new MfcSketchCurveReference(0x49, SketchCurveRole.Profile),
                new MfcSketchCurveReference(0x4C, SketchCurveRole.Profile)
            };

        foreach (var profile in profiles)
        {
            profile.Serialize(ar);
            MfcSketchCurveReference.WriteNullPlaceholder(ar);
            byte[] meta = new byte[84];
            ar.WriteBytes(meta);
        }
    }

    private void SerializeSecondaryCurves(MfcArchive ar)
    {
        ushort tag = 0;
        ar.ClassTagOrRef("moWireContact_c", ref tag);
        ushort schema = 0x8004;
        ar.U16(ref schema);

        if (CenterlineReference != null)
        {
            // Centerline writes 2 identical records
            CenterlineReference.Serialize(ar);
            MfcSketchCurveReference.WriteNullPlaceholder(ar);
            byte[] meta1 = new byte[86]; ar.WriteBytes(meta1);

            CenterlineReference.Serialize(ar);
            MfcSketchCurveReference.WriteNullPlaceholder(ar);
            byte[] meta2 = new byte[86]; ar.WriteBytes(meta2);
        }
        else
        {
            foreach (var guide in GuideReferences)
            {
                guide.Serialize(ar);
                MfcSketchCurveReference.WriteNullPlaceholder(ar);
                byte[] meta = new byte[86];
                ar.WriteBytes(meta);
            }
        }
    }

    private void SerializeTangencyTail(MfcArchive ar)
    {
        uint startVal = 0x00000100u | (uint)((StartTangencyType ?? 0) & 0xFF);
        uint endVal = 0x00000100u | (uint)((EndTangencyType ?? 0) & 0xFF);
        ar.U32(ref startVal);
        ar.U32(ref endVal);
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

        // Find moCompProfile_c
        int profileTag = MfcPrimitives.FindAscii(rem, "moCompProfile_c");
        if (profileTag >= 0)
        {
            int dataStart = profileTag + "moCompProfile_c".Length + 2;
            int offset = dataStart;
            ProfileReferences.Clear();
            int profileIdx = 0;
            while (offset + 188 <= rem.Length)
            {
                uint magic = BitConverter.ToUInt32(rem, offset);
                if (magic != MfcSketchCurveReference.RefMagic) break;
                uint entId = BitConverter.ToUInt32(rem, offset + 4);
                if (entId == 0) break;

                ushort headerId = (profileIdx < SelectionReferenceIds.Count) ? SelectionReferenceIds[profileIdx] : (ushort)0;
                var pref = new MfcSketchCurveReference(entId, SketchCurveRole.Profile, headerId);
                ProfileReferences.Add(pref);
                offset += 188;
                profileIdx++;
            }
            if (ProfileReferences.Count > 0)
                Resolution.Profiles = true;
        }

        // Find moWireContact_c
        int wireTag = MfcPrimitives.FindAscii(rem, "moWireContact_c");
        if (wireTag >= 0)
        {
            int dataStart = wireTag + "moWireContact_c".Length + 2;
            int offset = dataStart;
            var wireRefs = new List<MfcSketchCurveReference>();
            int guideIdx = ProfileReferences.Count;
            while (offset + 190 <= rem.Length)
            {
                uint magic = BitConverter.ToUInt32(rem, offset);
                if (magic != MfcSketchCurveReference.RefMagic) break;
                uint entId = BitConverter.ToUInt32(rem, offset + 4);
                if (entId == 0) break;

                ushort headerId = (guideIdx < SelectionReferenceIds.Count) ? SelectionReferenceIds[guideIdx] : (ushort)0;
                var gref = new MfcSketchCurveReference(entId, SketchCurveRole.Guide, headerId);
                wireRefs.Add(gref);
                offset += 190;
                guideIdx++;
            }

            if (wireRefs.Count == 2 && wireRefs[0].EntityId == wireRefs[1].EntityId)
            {
                Centerline = new SketchCurveReference
                {
                    Role = SketchCurveRole.Centerline,
                    EntityId = wireRefs[1].EntityId,
                    ArchiveSerial = wireRefs[1].ArchiveSerial,
                    HeaderSelectionId = wireRefs[1].HeaderSelectionId,
                    IsHeaderSelectionIdPositional = false
                };
                Resolution.Centerline = true;
            }
            else
            {
                foreach (var g in wireRefs)
                    GuideReferences.Add(g);
                if (GuideReferences.Count > 0)
                    Resolution.GuideCurves = true;
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

        // Tangency tail (8 bytes preceding thinTag if thinTag exists, else 8 bytes before end of rem)
        int tailEnd = (thinTag >= 6) ? (thinTag - 6) : rem.Length;
        if (tailEnd >= 8)
        {
            uint startVal = BitConverter.ToUInt32(rem, tailEnd - 8);
            uint endVal = BitConverter.ToUInt32(rem, tailEnd - 4);
            if ((startVal & 0xFFFFFF00) == 0x00000100 && (endVal & 0xFFFFFF00) == 0x00000100)
            {
                StartTangencyType = (int)(startVal & 0xFF);
                EndTangencyType = (int)(endVal & 0xFF);
                Resolution.StartTangencyType = true;
                Resolution.EndTangencyType = true;
            }
        }

        RawPayload = rem;
    }

    internal static LoftFeatureModel? Parse(MfcElement? element)
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
            var model = new LoftFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.DisplayName && model.Version == 2.0 && model.SerializedTypeId.HasValue)
                return model;
        }
        catch { }

        var selection = SweepLoftSelectionDecoder.Decode(element);
        if (selection == null)
            selection = TryDecodeLoftCenterlineDirect(element);
        if (selection != null && selection.Kind == SweepLoftSelectionKind.Loft)
        {
            int? startTangency = null;
            int? endTangency = null;
            if (selection.SerializedTypeCode == 0x4D &&
                TryReadTangencyTypes(element, out int start, out int end))
            {
                startTangency = start;
                endTangency = end;
            }
            var centerline = ReadLoftCenterline(element);

            return new LoftFeatureModel(
                element.AsWChar() ?? string.Empty,
                selection.SerializedTypeCode,
                selection.HeaderSelectionIds,
                selection.References,
                selection.CircularProfileLayout,
                selection.CircularProfileDiameter,
                selection.ThinFeatureLayout,
                selection.ThinWallThickness,
                selection.IsHeaderSelectionIdMappingPositional,
                startTangency,
                endTangency,
                centerline);
        }

        return null;
    }

    private static SweepLoftSelectionData? TryDecodeLoftCenterlineDirect(MfcElement element)
    {
        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: element.ClassName,
            minTypeId: 0x4D,
            maxTypeId: 0x50,
            requireEmptyWCharAt10: true);
        if (ctx == null || !ctx.TryReadHeaderSelectionIds(out var headerIds))
            return null;

        var primary = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moCompProfile_c" && t.TagOffset > ctx.Start);
        var secondary = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moWireContact_c" && t.TagOffset > ctx.Start);
        if (primary == null || secondary == null)
            return null;

        var profiles = ctx.Selections.ReadSketchRefChain(primary.DataStart + 81, 188, 8);
        var secondaries = ctx.Selections.ReadSketchRefChain(secondary.DataStart + 147, 190, 8);
        if (profiles.Count < 2 || secondaries.Count != 2 ||
            secondaries[0].Id != secondaries[1].Id ||
            headerIds.Count != profiles.Count + 1)
        {
            return null;
        }

        var refs = new List<SketchCurveReference>();
        for (int i = 0; i < profiles.Count; i++)
        {
            refs.Add(new SketchCurveReference
            {
                Role = SketchCurveRole.Profile,
                EntityId = profiles[i].Id,
                ArchiveSerial = profiles[i].Serial,
                HeaderSelectionId = headerIds[i],
                IsHeaderSelectionIdPositional = true
            });
        }
        refs.Add(new SketchCurveReference
        {
            Role = SketchCurveRole.Guide,
            EntityId = secondaries[0].Id,
            ArchiveSerial = secondaries[0].Serial,
            HeaderSelectionId = headerIds[profiles.Count],
            IsHeaderSelectionIdPositional = true
        });
        return new SweepLoftSelectionData
        {
            Kind = SweepLoftSelectionKind.Loft,
            ClassName = element.ClassName,
            SerializedTypeCode = ctx.TypeId,
            HeaderSelectionIds = headerIds,
            References = refs.AsReadOnly()
        };
    }

    private static SketchCurveReference? ReadLoftCenterline(MfcElement element)
    {
        var doc = element.Document ?? MfcDocument.Parse(element.SourceData);
        var wire = doc.AllElements
            .FirstOrDefault(t => t.ClassName == "moWireContact_c" && t.TagOffset > element.TagOffset);
        if (wire == null)
            return null;

        byte[] data = element.SourceData;
        int start = wire.DataStart;
        if (start < 0 || start + 52 > data.Length)
            return null;

        var records = new List<(uint Id, uint Serial)>();
        for (int pos = start; pos + 52 <= data.Length; pos++)
        {
            if (FeatureSelectionBag.TryReadSketchRefAt(data, pos, out uint id, out uint serial))
                records.Add((id, serial));
        }
        if (records.Count < 2)
            return null;

        var second = records[1];
        return new SketchCurveReference
        {
            Role = SketchCurveRole.Centerline,
            EntityId = second.Id,
            ArchiveSerial = second.Serial,
            HeaderSelectionId = 0,
            IsHeaderSelectionIdPositional = false
        };
    }

    private static bool TryReadTangencyTypes(MfcElement element, out int start, out int end)
    {
        start = 0;
        end = 0;
        int offset = element.DataEnd + 0x1A8;
        if (offset < 0 || offset + 8 > element.SourceData.Length)
            return false;
        uint first = BitConverter.ToUInt32(element.SourceData, offset);
        uint second = BitConverter.ToUInt32(element.SourceData, offset + 4);
        if ((first & 0xFFFFFF00) != 0x00000100 || (first & 0xFF) > 1 ||
            (second & 0xFFFFFF00) != 0x00000100 || (second & 0xFF) > 1)
            return false;
        start = (int)(first & 0xFF);
        end = (int)(second & 0xFF);
        return true;
    }
}
