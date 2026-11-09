using System;
using System.Linq;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Reads the design-tree <c>moSurfaceBodyFolder_c</c> node as a presence-level
/// surface-body ownership marker. The 2026 controlled samples serialize no individual
/// surface-body ids in the ResolvedFeatures stream, so only the validated folder header
/// is exposed; any other layout returns null.
/// </summary>
internal static class SurfaceBodyFolderParser
{
    internal const string ClassName = "moSurfaceBodyFolder_c";
    internal const uint ExpectedTypeId = 0x0A;
    internal const uint ExpectedChildFlag = 4;
    internal const uint ExpectedSubType = 2;
    internal const uint ExpectedHasData = 0x00010000;

    internal static SurfaceBodyFolderReference? TryParse(FeatureWindowContext ctx) =>
        ctx == null ? null : TryParse(ctx.Document);

    internal static SurfaceBodyFolderReference? TryParse(MfcDocument doc)
    {
        if (doc == null || doc.SourceData.Length < 16)
            return null;
        var matches = doc.AllElements.Where(e => e.ClassName == ClassName).ToList();
        if (matches.Count != 1)
            return null;
        var element = matches[0];
        if (element.TagByte != 0x01 || element.DataStart < 0 || element.DataEnd <= element.DataStart)
            return null;

        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        if (!header.IsValid ||
            header.TypeId != ExpectedTypeId ||
            header.ChildFlag != ExpectedChildFlag ||
            header.SubType != ExpectedSubType ||
            header.VersionFlag != 0x00010000 ||
            header.HasData != ExpectedHasData ||
            header.MarkerE == 0)
            return null;

        return new SurfaceBodyFolderReference
        {
            SerializedTypeId = header.TypeId,
            MarkerE = header.MarkerE,
            ChildFlag = header.ChildFlag,
            SubType = header.SubType,
            VersionFlag = header.VersionFlag,
            HasData = header.HasData
        };
    }

    internal static SurfaceBodyFolderReference? TryParse(byte[] source) =>
        source == null || source.Length < 16 ? null : TryParse(MfcDocument.Parse(source));
}
