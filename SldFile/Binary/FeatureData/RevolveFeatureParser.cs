using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Strict reader for the measured revolution layouts:
/// <list type="bullet">
/// <item><c>moRevolution_c</c> / TypeId 0x4E / stream 0x70: single-direction solid (mode 1, D1 angle)
/// and both-directions solid (mode 0, D1 + D2 angles, Type=2).</item>
/// <item><c>moRevolutionThin_c</c> / TypeId 0x50 / stream 0x70: thin one-direction (empty
/// moThinSpec_c payload, D1 angle, D3/D4 wall thicknesses).</item>
/// <item><c>moRevCut_c</c> / TypeId 0x55 / stream 0x71: cut full revolution (D1 angle, IsCut).</item>
/// </list>
/// Unknown layouts return null; nothing is inferred from an approximate value or a default.
/// </summary>
internal static class RevolveFeatureParser
{
    private const string SolidClass = "moRevolution_c";
    private const string ThinClass = "moRevolutionThin_c";
    private const string CutClass = "moRevCut_c";
    private const uint SolidTypeId = 0x4E;
    private const uint ThinTypeId = 0x50;
    private const uint CutTypeId = 0x55;

    internal static RevolveFeatureDefinitionData? Parse(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        var source = doc.SourceData;
        if (source.Length < 2) return null;
        ushort version = BitConverter.ToUInt16(source, 0);
        if (className == SolidClass && typeId == CutTypeId && version == 0x71)
            return ParseAxisEdge(doc, start, end);
        bool solid = className == SolidClass && typeId == SolidTypeId;
        bool thin = className == ThinClass && typeId == ThinTypeId;
        bool cut = className == CutClass && typeId == CutTypeId;
        if (!solid && !thin && !cut) return null;
        if (cut ? version != 0x71 : version != 0x70) return null;

        int limit = Math.Min(end, source.Length);
        var nodes = doc.AllElements.Where(e => e.TagOffset >= start && e.TagOffset < limit).ToList();
        var specs = nodes.Where(e => e.ClassName == "moRevEndSpec_c").ToList();
        var angles = nodes.Where(e => e.ClassName == "moAngleParameter_c").ToList();
        var axes = nodes.Where(e => e.ClassName == "moLineRef_w").ToList();
        if (specs.Count != 1 || angles.Count != 1 || axes.Count != 1) return null;

        int spec = specs[0].DataStart;
        if (spec < 0 || spec + 28 > limit) return null;
        uint mode = BitConverter.ToUInt32(source, spec);
        if (mode > 1) return null;
        for (int i = spec + 4; i < spec + 28; i++)
            if (source[i] != 0) return null;
        bool both = mode == 0;
        // Both directions is only measured on the solid moRevolution_c class.
        if (both && !solid) return null;

        int index = IndexOf(nodes, angles[0]);
        if (index < 0) return null;
        if (cut)
        {
            // Cut layout: moDisplayAngularDim_c -> AngleDim_c -> moAngleParameter_c.
            if (index < 2 ||
                nodes[index - 1].ClassName != "AngleDim_c" ||
                nodes[index - 2].ClassName != "moDisplayAngularDim_c" ||
                nodes[index - 2].TagOffset <= specs[0].TagOffset) return null;
        }
        else
        {
            // Solid/thin layout: display -> feature-dim handle -> AngleDim -> angle parameter.
            if (index < 3 ||
                nodes[index - 1].ClassName != "AngleDim_c" ||
                nodes[index - 2].ClassName != "moFeatureDimHandle_c" ||
                nodes[index - 3].ClassName != "moDisplayAngularDim_c" ||
                nodes[index - 3].TagOffset <= specs[0].TagOffset) return null;
        }

        var angleValue = ExtrudeAdditionalOptionParser.ReadNamedParameter(source, angles[0].DataStart);
        if (!angleValue.HasValue || !IsAngle(angleValue.Value)) return null;

        var d1 = ExtrudeAdditionalOptionParser.FindNamedParameters(source, start, limit, "D1");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(source, start, limit, "D2");
        var d3 = ExtrudeAdditionalOptionParser.FindNamedParameters(source, start, limit, "D3");
        var d4 = ExtrudeAdditionalOptionParser.FindNamedParameters(source, start, limit, "D4");
        if (d1.Count != 1 || Math.Abs(d1[0].Value - angleValue.Value) > 1e-12) return null;

        double angleDirection2 = 0;
        if (both)
        {
            if (d2.Count != 1 || !IsAngle(d2[0].Value)) return null;
            angleDirection2 = d2[0].Value;
        }
        else if (d2.Count != 0)
        {
            return null;
        }

        double thickness1 = 0;
        double thickness2 = 0;
        if (thin)
        {
            // One-direction thin is serialized with an empty moThinSpec_c payload, immediately
            // followed by the display-distance/parallel-plane/length-parameter objects.
            var thinSpecs = nodes.Where(e => e.ClassName == "moThinSpec_c").ToList();
            if (thinSpecs.Count != 1) return null;
            int thinIndex = IndexOf(nodes, thinSpecs[0]);
            if (thinIndex < 0 || thinIndex + 1 >= nodes.Count ||
                nodes[thinIndex + 1].TagOffset != thinSpecs[0].DataStart) return null;
            if (nodes.Count(e => e.ClassName == "moDisplayDistanceDim_c") != 1 ||
                nodes.Count(e => e.ClassName == "ParallelPlaneDistanceDim_c") != 1 ||
                nodes.Count(e => e.ClassName == "moLengthParameter_c") != 1) return null;
            if (d3.Count != 1 || d4.Count != 1) return null;
            if (!IsPositiveFinite(d3[0].Value) || !IsNonNegativeFinite(d4[0].Value)) return null;
            thickness1 = d3[0].Value;
            thickness2 = d4[0].Value;
        }
        else if (d3.Count != 0 || d4.Count != 0)
        {
            return null;
        }

        int axis = axes[0].TagOffset;
        if (axis - 32 < start || axis >= specs[0].TagOffset) return null;
        for (int i = axis - 32; i < axis - 19; i++)
            if (source[i] != 0) return null;
        uint reverse = BitConverter.ToUInt32(source, axis - 19);
        ushort expectedAxisTag = cut ? (ushort)0x8065 : (ushort)0x8064;
        uint expectedAxisValue = thin ? 5u : 3u;
        if (reverse > 1 || source[axis - 15] != 0 || source[axis - 14] != 0 || source[axis - 13] != 0 ||
            BitConverter.ToUInt16(source, axis - 12) != expectedAxisTag ||
            BitConverter.ToUInt16(source, axis - 10) != 0 ||
            BitConverter.ToUInt32(source, axis - 8) != expectedAxisValue ||
            BitConverter.ToUInt32(source, axis - 4) != 1) return null;

        var definition = new RevolveFeatureDefinitionData
        {
            Angle = angleValue.Value,
            AngleDirection2 = angleDirection2,
            Type = both ? 2 : 0,
            BothDirections = both,
            ReverseDirection = reverse == 1,
            IsCut = cut,
            IsThinFeature = thin,
            ThinWallType = 0,
            Thickness1 = thickness1,
            Thickness2 = thickness2
        };
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.Angle));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.AngleDirection2));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.Type));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.BothDirections));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.ReverseDirection));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.IsCut));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.IsThinFeature));
        if (thin)
        {
            definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.ThinWallType));
            definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.Thickness1));
            definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.Thickness2));
        }
        return definition;
    }

    /// <summary>
    /// Task 1.1 measured solid revolution with a model-edge axis: class moRevolution_c,
    /// FolderHeader TypeId 0x55, stream 0x71. Nodes are header, moCompSolidBody_c, an empty
    /// moLineRef_w, moCompEdge_c, moRevEndSpec_c and the display/AngleDim/angle-parameter
    /// chain. D1 is the 2π angle; API axisType=1 and the axis is an Edge.
    /// </summary>
    private static RevolveFeatureDefinitionData? ParseAxisEdge(MfcDocument doc, int start, int end)
    {
        var source = doc.SourceData;
        int limit = Math.Min(end, source.Length);
        var nodes = doc.AllElements.Where(e => e.TagOffset >= start && e.TagOffset < limit).ToList();
        var specs = nodes.Where(e => e.ClassName == "moRevEndSpec_c").ToList();
        var angles = nodes.Where(e => e.ClassName == "moAngleParameter_c").ToList();
        var lines = nodes.Where(e => e.ClassName == "moLineRef_w").ToList();
        var edges = nodes.Where(e => e.ClassName == "moCompEdge_c").ToList();
        if (specs.Count != 1 || angles.Count != 1 || lines.Count != 1 || edges.Count != 1)
            return null;
        if (nodes.Any(e => e.ClassName == "moThinSpec_c")) return null;
        int spec = specs[0].DataStart;
        if (spec < 0 || spec + 28 > limit) return null;
        uint mode = BitConverter.ToUInt32(source, spec);
        if (mode > 1) return null;
        for (int i = spec + 4; i < spec + 28; i++) if (source[i] != 0) return null;

        int index = IndexOf(nodes, angles[0]);
        if (index < 2 ||
            nodes[index - 1].ClassName != "AngleDim_c" ||
            nodes[index - 2].ClassName != "moDisplayAngularDim_c" ||
            nodes[index - 2].TagOffset <= specs[0].TagOffset) return null;
        var angleValue = ExtrudeAdditionalOptionParser.ReadNamedParameter(source, angles[0].DataStart);
        if (!angleValue.HasValue || !IsAngle(angleValue.Value)) return null;
        var d1 = ExtrudeAdditionalOptionParser.FindNamedParameters(source, start, limit, "D1");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(source, start, limit, "D2");
        var d3 = ExtrudeAdditionalOptionParser.FindNamedParameters(source, start, limit, "D3");
        var d4 = ExtrudeAdditionalOptionParser.FindNamedParameters(source, start, limit, "D4");
        if (d1.Count != 1 || Math.Abs(d1[0].Value - angleValue.Value) > 1e-12 ||
            d2.Count != 0 || d3.Count != 0 || d4.Count != 0) return null;

        var definition = new RevolveFeatureDefinitionData
        {
            Angle = angleValue.Value,
            AngleDirection2 = 0,
            Type = 0,
            BothDirections = false,
            ReverseDirection = mode == 0,
            IsCut = false,
            IsThinFeature = false,
            Merge = true,
            FolderTypeId = 0x55u,
            AxisType = 1,
            AxisReference = new FeatureReferenceData
            {
                EntityType = SelectionEntityType.Edge,
                ReferenceName = "moCompEdge_c",
                RecordCount = 1
            }
        };
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.Angle));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.IsCut));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.IsThinFeature));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.FolderTypeId));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.AxisType));
        definition.MarkResolvedInternal(nameof(RevolveFeatureDefinitionData.AxisReference));
        return definition;
    }

    private static int IndexOf(List<MfcElement> nodes, MfcElement element)
    {
        for (int i = 0; i < nodes.Count; i++)
            if (ReferenceEquals(nodes[i], element)) return i;
        return -1;
    }

    private static bool IsAngle(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0 && value <= 2 * Math.PI;

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private static bool IsNonNegativeFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
}
