using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;


/// <summary>
/// Unified strongly-typed model for moMirrorPattern_c Mirror pattern/body feature.
/// Symmetrically reads and writes mirror plane, feature id, and body pattern flags via IMfcSerializable.
/// </summary>
public sealed class MirrorFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string MfcClassName = "moMirrorPattern_c";
    public const uint HeaderTypeId = 0x57u;
    public const ushort StreamVersion = 0x72;

    public const int PayloadLength = 1201;
    public const int HeaderRestOffset = 38;
    public const int HeaderRestLength = 1163;
    public const int FeatureIdOffsetInPayload = 147;
    public const int FeatureIdOffsetInRest = FeatureIdOffsetInPayload - HeaderRestOffset;
    public const int ResaveVariantRestOffset = 139;
    public const uint DefaultFeatureId = 0x1E5F19F1u;
    public const ushort StrippedFolderCount = 22;
    public const int SourceReferenceOffset = 360;
    public const int SourceReferenceLength = 120;
    public const int PlaneReferenceOffset = 514;

    public const string BaseSha256 =
        "8e911328454eb414ab917297f669cb4fc06ff89b1d187520f07f40efd259305e";

    public static readonly byte[] SourceReferenceData = PatternAuthoringSegments.MirrorSourceReferenceData;
    public static readonly byte[] PlaneReferenceData = new byte[] {
        0x01, 0x00, 0x00, 0x00, 0x6C, 0x80, 0x33, 0x00, 0x00, 0x00
    };
    public static readonly byte[] HeaderRest = PatternAuthoringSegments.MirrorHeaderRest;

    public string FeatureName { get; set; } = "镜像1";
    public double Version { get; set; } = 2.0;
    public uint TypeId { get; set; } = HeaderTypeId;
    public string ClassName => MfcClassName;
        
    public string MirrorPlaneName { get; set; } = "Right Plane";
    public uint FeatureId { get; set; } = DefaultFeatureId;
    public bool? PatternBodyOnly { get; set; }
    public bool? GeometryPattern { get; set; }

    public FeatureReferenceData? Plane { get; set; }
    public int? MirrorPlaneType { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFeatureArray { get; set; }
    public IReadOnlyList<FeatureReferenceData>? MirrorFaceArray { get; set; }
    public bool? PropagateVisualProperty { get; set; }
    public int? FeatureScope { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FeatureScopeBodies { get; set; }

    public int? GetMirrorPlaneType() => MirrorPlaneType;
    public int? GetPatternFeatureCount() => PatternFeatureArray?.Count;
    public int? GetMirrorFaceCount() => MirrorFaceArray?.Count;
    public int? GetFeatureScopeBodiesCount() => FeatureScopeBodies?.Count;

    public MirrorFeatureModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref refTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName;
            ar.WChar(ref name);

            double v = Version;
            ar.F64(ref v);

            uint tid = TypeId;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = string.Empty;
            ar.WChar(ref empty);

            string? mp = MirrorPlaneName;
            ar.WChar(ref mp);

            uint fid = FeatureId;
            ar.U32(ref fid);

            bool bo = PatternBodyOnly ?? false;
            ar.Boolean(ref bo);

            bool gp = GeometryPattern ?? false;
            ar.Boolean(ref gp);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

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

            if (ar.Remaining > 0)
            {
                string? mp = null;
                ar.WChar(ref mp);
                if (mp != null) MirrorPlaneName = mp;
            }

            if (ar.Remaining >= 4)
            {
                uint fid = 0;
                ar.U32(ref fid);
                FeatureId = fid;
            }

            if (ar.Remaining >= 1)
            {
                bool bo = false;
                ar.Boolean(ref bo);
                PatternBodyOnly = bo;
            }

            if (ar.Remaining >= 1)
            {
                bool gp = false;
                ar.Boolean(ref gp);
                GeometryPattern = gp;
            }
        }
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/MirrorPatternSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured mirror-pattern layout from the SW2026 PatternOptions/mirror_plane
/// control and its resave: moMirrorPattern_c / FolderHeader TypeId 0x57,
/// stream 0x72. The payload contains the source-feature persistent reference
/// blob (five records for BaseBox), the Right-Plane reference block and the
/// mirror flags/transform tail. The template is emitted only on the exact
/// extrusion-stripped base pinned by the normalized SHA-256 signature.
/// </summary>
internal static class MirrorPatternSegments
{
    internal const string ClassName = "moMirrorPattern_c";
    internal const uint TypeId = 0x57u;
    internal const int PayloadLength = 1201;
    internal const int HeaderRestOffset = 38;
    internal const int HeaderRestLength = 1163;
    internal const int FeatureIdOffsetInPayload = 147;
    internal const int FeatureIdOffsetInRest = FeatureIdOffsetInPayload - HeaderRestOffset;
    /// <summary>Main vs resave rest differs at this archive byte; all other rest bytes are stable.</summary>
    internal const int ResaveVariantRestOffset = 139;
    internal const uint FeatureId = 0x1E5F19F1u;
    internal const ushort StrippedFolderCount = 22;
    internal const int SourceReferenceOffset = 360;
    internal const int SourceReferenceLength = 120;
    internal const int PlaneReferenceOffset = 514;

    internal const string BaseSha256 =
        "8e911328454eb414ab917297f669cb4fc06ff89b1d187520f07f40efd259305e";

    /// <summary>Measured source-feature persistent-reference blob (BaseBox; serial 0x6AE46BC8, ids 4/0/1/1/3).</summary>
    internal static readonly byte[] SourceReferenceData = SldFile.Mfc.PatternAuthoringSegments.MirrorSourceReferenceData;

    /// <summary>Measured Right-Plane reference block (01 00 00 00 | class ref 0x806C | 0x33 | 00 00).</summary>
    internal static readonly byte[] PlaneReferenceData = new byte[] {
        0x01, 0x00, 0x00, 0x00, 0x6C, 0x80, 0x33, 0x00, 0x00, 0x00
    };

    /// <summary>moMirrorPattern_c data after schema/name/double/TypeId 0x57/u32 0/empty WChar; feature id at +109.</summary>
    internal static readonly byte[] HeaderRest = SldFile.Mfc.PatternAuthoringSegments.MirrorHeaderRest;

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/MirrorPatternFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Controlled mirror-pattern authoring on the measured PatternOptions/mirror_plane base.
/// The target must be the native sample stripped at its moMirrorPattern_c tag (folder count
/// 22). The measured payload already carries the BaseBox source-feature persistent-reference
/// blob and the Right-Plane reference block; only the feature name and allocated feature id
/// are written. All other definition fields must stay at the measured/default values.
/// </summary>
internal static class MirrorPatternFeatureBuilder
{
    internal static byte[] BuildMirrorPatternStream(
        byte[] currentStream, MirrorFeatureModel definition, string featureName)
    {
        ValidateDefinition(definition);
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x72)
        {
            throw new NotSupportedException(
                $"Mirror pattern authoring requires a ResolvedFeatures stream version 0x72, but the document " +
                $"uses 0x{version:X2}. No stream changes have been made.");
        }
        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, MirrorPatternSegments.ClassName) > 0)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring supports only the stripped base without an existing mirror feature. " +
                "No stream changes have been made.");
        }
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        if (folderCount != MirrorPatternSegments.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Mirror pattern authoring requires the extrusion-stripped base with folder count " +
                $"{MirrorPatternSegments.StrippedFolderCount}, but the document uses {folderCount}. " +
                "No stream changes have been made.");
        }

        byte[] normalized = (byte[])currentStream.Clone();
        normalized[4] = 0;
        normalized[5] = 0;
        string sha;
        using (var algorithm = SHA256.Create())
            sha = BitConverter.ToString(algorithm.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
        if (!string.Equals(sha, MirrorPatternSegments.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Mirror pattern authoring is limited to the controlled PatternOptions/mirror_plane base " +
                $"(base signature {sha.Substring(0, 12)} does not match). No stream changes have been made.");
        }

        byte[] featureBlock = BuildFeatureBlock(featureName);

        ushort newFolderCount = (ushort)(folderCount + 1);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes(newFolderCount).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    internal static void ValidateDefinition(MirrorFeatureModel definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        if (definition.MirrorPlaneType.HasValue && definition.MirrorPlaneType.Value != 1)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring supports only the measured reference-plane layout " +
                "(MirrorPlaneType = 1). No stream changes have been made.");
        }
        if (definition.Plane != null)
        {
            if (definition.Plane.EntityType != SelectionEntityType.Plane ||
                definition.Plane.EntityId.HasValue || !string.IsNullOrEmpty(definition.Plane.ReferenceName))
            {
                throw new NotSupportedException(
                    "Mirror pattern authoring keeps the measured Right-Plane reference; only a presence-only " +
                    "Plane reference is accepted. No stream changes have been made.");
            }
        }
        if (definition.PatternFeatureArray != null)
        {
            if (definition.PatternFeatureArray.Count != 1 ||
                definition.PatternFeatureArray[0] == null ||
                definition.PatternFeatureArray[0].EntityType != SelectionEntityType.None ||
                definition.PatternFeatureArray[0].EntityId.HasValue ||
                !string.IsNullOrEmpty(definition.PatternFeatureArray[0].ReferenceName))
            {
                throw new NotSupportedException(
                    "Mirror pattern authoring keeps the measured BaseBox source feature; only one presence-only " +
                    "source reference is accepted. No stream changes have been made.");
            }
        }
        if (definition.GeometryPattern == true)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support GeometryPattern. No stream changes have been made.");
        }
        if (definition.FeatureScope.HasValue && definition.FeatureScope.Value != 0)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support feature scope. No stream changes have been made.");
        }
        if (definition.FeatureScopeBodies != null && definition.FeatureScopeBodies.Count > 0)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support feature-scope bodies. No stream changes have been made.");
        }
        if (definition.MirrorFaceArray != null && definition.MirrorFaceArray.Count > 0)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support mirror faces. No stream changes have been made.");
        }
        if (definition.PropagateVisualProperty.HasValue)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support PropagateVisualProperty. No stream changes have been made.");
        }
    }

    private static byte[] BuildFeatureBlock(string featureName)
    {
        if (string.IsNullOrEmpty(featureName)) featureName = "镜像1";
        byte[] rest = (byte[])MirrorPatternSegments.HeaderRest.Clone();
        BitConverter.GetBytes(MirrorPatternSegments.FeatureId)
            .CopyTo(rest, MirrorPatternSegments.FeatureIdOffsetInRest);

        using var writer = new MfcStreamWriter(2048);
        writer.WriteMfcTag(MirrorPatternSegments.ClassName);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(MirrorPatternSegments.TypeId);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(rest);
        return writer.ToArray();
    }
}


