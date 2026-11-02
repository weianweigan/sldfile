using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Mate authoring on a clean 2026 assembly base (MatesList version 0xAB, count 0,
/// stream body empty).
///
/// The single-mate stream is the natively verified v1 layout:
///   6-byte header (AB 00 00 00 | u16 mate count) + one measured mate record.
/// The embedded record's four entity selection strings are rewritten to the
/// caller's <c>feature@component@assembly</c> names (WChar lengths rebuilt); the
/// Distance record's D1 parameter value (and the positive copies inside its
/// moLengthParameter_c element) is patched to the requested distance.
///
/// The dual-mate path appends the measured legal two-record MatesList
/// (Coincident front/front + Distance right/right, count 2) and rewrites its
/// eight entity WChars and the Distance D1 value. Only the natively verified
/// cube-1/cylinder-1 front/right combination is accepted. Everything else is
/// rejected with <see cref="NotSupportedException"/> before any pending stream
/// is changed.
/// </summary>
internal static class MateAuthoringWriter
{
    private const ushort ExpectedStreamVersion = 0x00AB;
    private const int CleanHeaderLength = 6;

    internal static void AddMate(
        SldAssembly assembly,
        byte[] template,
        string entity1,
        string entity2,
        double? distance)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (template == null || template.Length == 0)
            throw new InvalidOperationException("The embedded mate template is empty.");
        if (assembly.IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        if (distance.HasValue && (double.IsNaN(distance.Value) || double.IsInfinity(distance.Value) ||
                                  distance.Value <= 0 || distance.Value > 1000))
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be a finite value in (0, 1000] meters.");

        string streamPath = FindMatesListStreamPath(assembly);
        byte[] current = assembly.GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        ValidateCleanBase(current);

        var first = ParseEntity(entity1, nameof(entity1));
        var second = ParseEntity(entity2, nameof(entity2));
        ValidateEntities(assembly, first, second);

        byte[] record = RewriteEntityStrings(template, entity1, entity2);
        if (distance.HasValue)
            PatchDistance(record, distance.Value);

        byte[] header = (byte[])current.Clone();
        BitConverter.GetBytes((ushort)1).CopyTo(header, 4);
        byte[] updated = new byte[header.Length + record.Length];
        Buffer.BlockCopy(header, 0, updated, 0, header.Length);
        Buffer.BlockCopy(record, 0, updated, header.Length, record.Length);

        // All validation and record construction completed: only now touch the
        // pending stream set.
        assembly.SetPendingStream(streamPath, updated);
    }

    // ── Dual-mate authoring ────────────────────────────────────────────

    /// <summary>
    /// Appends the natively verified dual-mate group (Coincident front/front and
    /// Distance right/right) to a clean 0xAB base. The embedded stream contains
    /// both mate records; the eight entity WChars and the Distance D1 value are
    /// rewritten. Only the measured cube-1/cylinder-1 component pair and the
    /// template's front/right plane names are accepted.
    /// </summary>
    internal static void AddMateGroup(
        SldAssembly assembly,
        byte[] template,
        string coincidentEntity1,
        string coincidentEntity2,
        string distanceEntity1,
        string distanceEntity2,
        double distance)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (template == null || template.Length <= CleanHeaderLength)
            throw new InvalidOperationException("The embedded dual-mate template is empty.");
        if (assembly.IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        if (double.IsNaN(distance) || double.IsInfinity(distance) || distance <= 0 || distance > 1000)
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be a finite value in (0, 1000] meters.");
        if (BitConverter.ToUInt16(template, 0) != ExpectedStreamVersion ||
            BitConverter.ToUInt16(template, 2) != 0 ||
            BitConverter.ToUInt16(template, 4) != 2)
            throw new InvalidOperationException("The embedded dual-mate template header is not recognized.");

        string streamPath = FindMatesListStreamPath(assembly);
        byte[] current = assembly.GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        ValidateCleanBase(current);

        var coincident1 = ParseEntity(coincidentEntity1, nameof(coincidentEntity1));
        var coincident2 = ParseEntity(coincidentEntity2, nameof(coincidentEntity2));
        var distance1 = ParseEntity(distanceEntity1, nameof(distanceEntity1));
        var distance2 = ParseEntity(distanceEntity2, nameof(distanceEntity2));

        var fields = ReadAtFields(template);
        if (fields.Count != 8 ||
            !IsComponentName(fields[0].Value) || !IsSelectionName(fields[1].Value, fields[0].Value) ||
            !IsComponentName(fields[2].Value) || !IsSelectionName(fields[3].Value, fields[2].Value) ||
            !IsComponentName(fields[4].Value) || !IsSelectionName(fields[5].Value, fields[4].Value) ||
            !IsComponentName(fields[6].Value) || !IsSelectionName(fields[7].Value, fields[6].Value))
            throw new InvalidOperationException("The embedded dual-mate template entity layout is not recognized.");

        string component0 = ComponentOf(fields[0].Value);
        string component1 = ComponentOf(fields[2].Value);
        if (!string.Equals(component0, ComponentOf(fields[4].Value), StringComparison.Ordinal) ||
            !string.Equals(component1, ComponentOf(fields[6].Value), StringComparison.Ordinal) ||
            string.Equals(component0, component1, StringComparison.Ordinal))
            throw new InvalidOperationException("The embedded dual-mate template does not use one component pair.");

        string frontFeature = FeatureOf(fields[1].Value);
        string rightFeature = FeatureOf(fields[5].Value);
        if (!string.Equals(frontFeature, "前视基准面", StringComparison.Ordinal) ||
            !string.Equals(rightFeature, "右视基准面", StringComparison.Ordinal))
            throw new InvalidOperationException("The embedded dual-mate template is not the verified front/right specimen.");

        var headerStrings = ReadModelHeaderStrings(assembly);
        string assemblyName = ReadSingleAssemblyName(headerStrings);
        ValidateGroupPair(coincident1, coincident2, frontFeature, component0, component1,
            headerStrings, assemblyName, "Coincident");
        ValidateGroupPair(distance1, distance2, rightFeature, component0, component1,
            headerStrings, assemblyName, "Distance");

        var coincident = OrderPairByTemplate(coincident1, coincident2, component0, component1);
        var distancePair = OrderPairByTemplate(distance1, distance2, component0, component1);
        string instance0 = coincident.Item1.Component + "@" + coincident.Item1.Assembly;
        string instance1 = coincident.Item2.Component + "@" + coincident.Item2.Assembly;
        var replacements = new[]
        {
            instance0,
            coincident.Item1.Feature + "@" + instance0,
            instance1,
            coincident.Item2.Feature + "@" + instance1,
            distancePair.Item1.Component + "@" + distancePair.Item1.Assembly,
            distancePair.Item1.Feature + "@" + distancePair.Item1.Component + "@" + distancePair.Item1.Assembly,
            distancePair.Item2.Component + "@" + distancePair.Item2.Assembly,
            distancePair.Item2.Feature + "@" + distancePair.Item2.Component + "@" + distancePair.Item2.Assembly
        };

        byte[] record = RebuildAtFields(template, fields, replacements);
        PatchDistance(record, distance);

        byte[] header = (byte[])current.Clone();
        BitConverter.GetBytes((ushort)2).CopyTo(header, 4);
        byte[] updated = new byte[header.Length + record.Length - CleanHeaderLength];
        Buffer.BlockCopy(header, 0, updated, 0, header.Length);
        Buffer.BlockCopy(record, CleanHeaderLength, updated, header.Length, record.Length - CleanHeaderLength);

        // All validation and record construction completed: only now touch the
        // pending stream set.
        assembly.SetPendingStream(streamPath, updated);
    }

    private static List<WCharField> ReadAtFields(byte[] data)
    {
        var fields = new List<WCharField>();
        int i = 0;
        while (i + 4 <= data.Length)
        {
            if (MfcPrimitives.IsWCharPrefix(data, i))
            {
                int cursor = i;
                string? value = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
                if (value != null && value.Length > 0 && value.IndexOf('@') >= 0)
                {
                    fields.Add(new WCharField { Offset = i, EndOffset = cursor, Value = value });
                    i = Math.Max(i, cursor - 1);
                }
            }
            i++;
        }
        return fields;
    }

    private static string ComponentOf(string selectionName)
    {
        int first = selectionName.IndexOf('@');
        return first > 0 ? selectionName.Substring(0, first) : string.Empty;
    }

    private static string FeatureOf(string selectionName)
    {
        int first = selectionName.IndexOf('@');
        return first > 0 ? selectionName.Substring(0, first) : string.Empty;
    }

    private static string ReadSingleAssemblyName(HashSet<string> headerStrings)
    {
        var paths = headerStrings
            .Where(v => v.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (paths.Length != 1)
            throw new NotSupportedException("The target base must contain exactly one assembly document path.");
        string name = Path.GetFileNameWithoutExtension(paths[0]);
        if (string.IsNullOrEmpty(name) || !headerStrings.Contains(name))
            throw new NotSupportedException("The target base does not expose a usable assembly document name.");
        return name;
    }

    private static void ValidateGroupPair(
        EntityParts first,
        EntityParts second,
        string expectedFeature,
        string component0,
        string component1,
        HashSet<string> headerStrings,
        string assemblyName,
        string label)
    {
        if (!string.Equals(first.Assembly, assemblyName, StringComparison.Ordinal) ||
            !string.Equals(second.Assembly, assemblyName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"The {label} selections must belong to the target assembly '{assemblyName}'.");
        if (!string.Equals(first.Feature, expectedFeature, StringComparison.Ordinal) ||
            !string.Equals(second.Feature, expectedFeature, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Only the verified '{expectedFeature}' {label} plane pair is supported.");
        bool pair = (string.Equals(first.Component, component0, StringComparison.Ordinal) &&
                     string.Equals(second.Component, component1, StringComparison.Ordinal)) ||
                    (string.Equals(first.Component, component1, StringComparison.Ordinal) &&
                     string.Equals(second.Component, component0, StringComparison.Ordinal));
        if (!pair)
            throw new NotSupportedException(
                $"Only the verified '{component0}'/'{component1}' component pair is supported.");
        if (!headerStrings.Contains(first.Component) || !headerStrings.Contains(second.Component))
            throw new NotSupportedException("A selected component instance is not present in the target assembly tree.");
    }

    private static (EntityParts First, EntityParts Second) OrderPairByTemplate(
        EntityParts first,
        EntityParts second,
        string component0,
        string component1)
    {
        if (string.Equals(first.Component, component0, StringComparison.Ordinal) &&
            string.Equals(second.Component, component1, StringComparison.Ordinal))
            return (first, second);
        if (string.Equals(first.Component, component1, StringComparison.Ordinal) &&
            string.Equals(second.Component, component0, StringComparison.Ordinal))
            return (second, first);
        throw new NotSupportedException("The entity pair does not match the template component slots.");
    }

    private static byte[] RebuildAtFields(byte[] template, List<WCharField> fields, string[] replacements)
    {
        if (replacements.Length != fields.Count)
            throw new InvalidOperationException("The dual-mate template replacement count does not match its entity fields.");
        var output = new List<byte>(template.Length + 64);
        int position = 0;
        for (int f = 0; f < fields.Count; f++)
        {
            var field = fields[f];
            output.AddRange(template.Skip(position).Take(field.Offset - position));
            output.AddRange(EncodeWChar(replacements[f]));
            position = field.EndOffset;
        }
        output.AddRange(template.Skip(position));
        return output.ToArray();
    }

    // ── Measured Width / RackPinion templates ─────────────────────────

    /// <summary>
    /// Patches the measured dimensioned <c>moMateWidth</c> record in place:
    /// D1 DistanceFromEnd (and its positive copies) plus the measured
    /// FlipDimension sign bytes. The base must contain exactly one dimensioned
    /// width mate with the measured layout (display dimension + length parameter
    /// + four deferred planar-face references).
    /// </summary>
    internal static void SetWidthMateDimension(SldAssembly assembly, double distance, bool flipDimension)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (assembly.IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        if (!(Math.Abs(distance - 0.01) < 1e-12 || Math.Abs(distance - 0.02) < 1e-12))
            throw new ArgumentOutOfRangeException(nameof(distance),
                "Only the measured 0.01 m and 0.02 m width dimensions are supported.");

        string streamPath = FindMatesListStreamPath(assembly);
        byte[] current = assembly.GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        var tags = MfcPrimitives.ScanMfcTags(current);
        var widthTags = tags.Where(t => t.ClassName == "moMateWidth").ToArray();
        if (widthTags.Length != 1)
            throw new NotSupportedException("Only a single measured moMateWidth mate is supported.");
        var widthTag = widthTags[0];
        var displayTags = tags.Where(t =>
            t.ClassName == "moDisplayWidthDim_c" && t.TagOffset > widthTag.TagOffset).ToArray();
        if (displayTags.Length != 1)
            throw new NotSupportedException("The width mate is not the measured dimensioned layout.");
        var lengthTags = tags.Where(t =>
            t.ClassName == "moLengthParameter_c" && t.TagOffset > widthTag.TagOffset).ToArray();
        if (lengthTags.Length != 1 ||
            !MateStructureDecoder.TryReadD1Record(current, lengthTags[0].DataStart, current.Length, out _))
            throw new NotSupportedException("The width mate has no measured D1 distance parameter.");

        byte[] template = flipDimension
            ? (byte[])WidthAuthoringTemplates.FlipTrueList.Clone()
            : (byte[])WidthAuthoringTemplates.FlipFalseList.Clone();
        if (template.Length != (flipDimension
                ? WidthAuthoringTemplates.FlipTrueLength
                : WidthAuthoringTemplates.FlipFalseLength) ||
            BitConverter.ToUInt16(template, 0) != WidthAuthoringTemplates.StreamVersion)
            throw new InvalidOperationException("The embedded Width template header is not recognized.");
        var templateTags = MfcPrimitives.ScanMfcTags(template);
        var templateLengthTags = templateTags
            .Where(t => t.ClassName == "moLengthParameter_c")
            .ToArray();
        if (templateLengthTags.Length != 1)
            throw new InvalidOperationException("The embedded Width template has no unique D1 distance parameter.");
        var templateLength = templateLengthTags[0];
        if (!MateStructureDecoder.TryReadD1Record(template, templateLength.DataStart, template.Length, out double oldValue))
            throw new InvalidOperationException("The embedded Width template D1 record is malformed.");
        int nextTag = templateTags
            .Where(t => t.TagOffset > templateLength.TagOffset)
            .Select(t => t.TagOffset)
            .DefaultIfEmpty(template.Length)
            .Min();
        int start = templateLength.DataStart;
        for (int i = start; i + 8 <= nextTag; i += 4)
        {
            double value = BitConverter.ToDouble(template, i);
            if (value == oldValue)
                BitConverter.GetBytes(distance).CopyTo(template, i);
        }
        BitConverter.GetBytes(distance).CopyTo(template, start + 32);

        assembly.SetPendingStream(streamPath, template);
    }

    /// <summary>
    /// Adds the measured RackPinion template to the clean three-mate
    /// rack-and-pinion base. The full natively verified four-mate MatesList is
    /// written (the other three measured mates remain); the RackPinion D1,
    /// diameter option and reverse flag are patched. Only the measured
    /// rack-1/spurgear-1 component pair is supported.
    /// </summary>
    internal static void AddRackPinionMate(
        SldAssembly assembly,
        string rackComponent,
        string pinionComponent,
        int diameterTypeCode,
        double diameterValue,
        bool reverse)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (assembly.IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        if (diameterTypeCode != 0 && diameterTypeCode != 1)
            throw new ArgumentOutOfRangeException(nameof(diameterTypeCode), "Only pitch diameter (0) and rack travel (1) are supported.");
        if (double.IsNaN(diameterValue) || double.IsInfinity(diameterValue) ||
            diameterValue <= 0 || diameterValue > 1000)
            throw new ArgumentOutOfRangeException(nameof(diameterValue), "Diameter must be a finite value in (0, 1000] meters.");
        const string rackName = "rack-1@rack and pinion";
        const string pinionName = "spurgear-1@rack and pinion";
        if (!string.Equals(rackComponent, rackName, StringComparison.Ordinal) ||
            !string.Equals(pinionComponent, pinionName, StringComparison.Ordinal))
            throw new NotSupportedException("Only the measured rack-1@rack and pinion / spurgear-1@rack and pinion pair is supported.");

        string streamPath = FindMatesListStreamPath(assembly);
        byte[] current = assembly.GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        if (current.Length < 6 ||
            BitConverter.ToUInt16(current, 0) != RackPinionAuthoringTemplates.StreamVersion ||
            BitConverter.ToUInt16(current, 4) != 6)
            throw new NotSupportedException("The base is not the measured clean three-mate rack-and-pinion assembly.");
        var currentTags = MfcPrimitives.ScanMfcTags(current);
        if (currentTags.Any(t => t.ClassName == "moMateRackPinionDim_c"))
            throw new NotSupportedException("The assembly already contains a RackPinion mate.");
        if (!currentTags.Any(t => t.ClassName == "moMateCoincident") ||
            !currentTags.Any(t => t.ClassName == "moMateDistanceDim_c") ||
            !currentTags.Any(t => t.ClassName == "moMateParallel"))
            throw new NotSupportedException("The base does not carry the measured three-mate rack-and-pinion layout.");

        byte[] template = (byte[])RackPinionAuthoringTemplates.FullList.Clone();
        if (template.Length != RackPinionAuthoringTemplates.FullListLength ||
            BitConverter.ToUInt16(template, 0) != RackPinionAuthoringTemplates.StreamVersion ||
            BitConverter.ToUInt16(template, 4) != 7)
            throw new InvalidOperationException("The embedded RackPinion template header is not recognized.");

        var headerStrings = ReadModelHeaderStrings(assembly);
        if (!headerStrings.Contains("rack-1") || !headerStrings.Contains("spurgear-1") ||
            !headerStrings.Contains("rack and pinion"))
            throw new NotSupportedException("The measured rack/pinion components are not present in the target assembly tree.");

        var templateTags = MfcPrimitives.ScanMfcTags(template);
        var rackTemplateTag = templateTags.FirstOrDefault(t => t.ClassName == "moMateRackPinionDim_c");
        if (rackTemplateTag == null)
            throw new InvalidOperationException("The embedded RackPinion template has no RackPinion record.");
        byte[] d1Pattern = { 0x06, 0x80, 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x31, 0x00 };
        int d1 = FindPattern(template, d1Pattern, rackTemplateTag.TagOffset);
        if (d1 < 0 || FindPattern(template, d1Pattern, d1 + 1) >= 0)
            throw new NotSupportedException("The embedded RackPinion template D1 record is not unique.");
        if (!MateStructureDecoder.TryReadD1Record(template, d1, template.Length, out double oldD1))
            throw new InvalidOperationException("The embedded RackPinion template D1 record is malformed.");
        int d1End = templateTags
            .Where(t => t.TagOffset > d1)
            .Select(t => t.TagOffset)
            .DefaultIfEmpty(template.Length)
            .Min();
        double newD1 = diameterTypeCode == 0 ? diameterValue : diameterValue / Math.PI;
        if (newD1 <= 0 || newD1 > 1000)
            throw new ArgumentOutOfRangeException(nameof(diameterValue), "The resulting D1 value is outside (0, 1000].");
        for (int i = d1; i + 8 <= d1End; i += 4)
        {
            double value = BitConverter.ToDouble(template, i);
            if (value == oldD1)
                BitConverter.GetBytes(newD1).CopyTo(template, i);
        }
        BitConverter.GetBytes(newD1).CopyTo(template, d1 + 32);

        BitConverter.GetBytes((uint)diameterTypeCode).CopyTo(template, template.Length - 4);
        int reverseFlag = FindRackPinionReverseFlagOffset(template);
        if (reverseFlag < 0)
            throw new InvalidOperationException("The embedded RackPinion template reverse flag is not unique.");
        template[reverseFlag + 4] = reverse ? (byte)0 : (byte)1;

        assembly.SetPendingStream(streamPath, template);
    }

    /// <summary>
    /// Writes the measured tutorial Path MatesList (four free moMatePath_c mates
    /// plus the tutorial Hinge/Concentric/Coincident/Parallel mates) onto the
    /// strict clean path base. The base must be the measured tutorial assembly
    /// with the four PathMate records removed (version 0x00E6, u16@4=12); no
    /// parameter/entity rewriting is performed.
    /// </summary>
    internal static void AddTutorialPathMates(SldAssembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (assembly.IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        string streamPath = FindMatesListStreamPath(assembly);
        byte[] current = assembly.GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        if (current.Length != PathAuthoringTemplates.CleanBaseLength ||
            BitConverter.ToUInt16(current, 0) != PathAuthoringTemplates.StreamVersion ||
            BitConverter.ToUInt16(current, 4) != PathAuthoringTemplates.CleanBaseTagCount)
            throw new NotSupportedException("The base is not the measured clean motion_path_tutorial assembly.");
        var tags = MfcPrimitives.ScanMfcTags(current);
        if (tags.Any(t => t.ClassName == "moMatePath_c"))
            throw new NotSupportedException("The assembly already contains a Path mate.");
        var headerStrings = ReadModelHeaderStrings(assembly);
        if (!headerStrings.Contains("chainpath-1") || !headerStrings.Contains("motion_path_tutorial"))
            throw new NotSupportedException("The measured chainpath/motion_path_tutorial components are not present in the target assembly tree.");

        byte[] template = (byte[])PathAuthoringTemplates.FullList.Clone();
        if (template.Length != PathAuthoringTemplates.FullListLength ||
            BitConverter.ToUInt16(template, 0) != PathAuthoringTemplates.StreamVersion ||
            BitConverter.ToUInt16(template, 4) != PathAuthoringTemplates.FullListTagCount)
            throw new InvalidOperationException("The embedded Path template header is not recognized.");
        assembly.SetPendingStream(streamPath, template);
    }

    /// <summary>
    /// Writes the measured chain_link1 Hinge template (4 Hinge records) onto the
    /// strict clean chain_link1 base (MatesList version 0x00E7, u16@4=0, 6 bytes).
    /// </summary>
    internal static void AddTutorialHingeMates(SldAssembly assembly)
    {
        ValidateMechanicalCleanBase(assembly, "moMateHinge", "chain_link1",
            MechanicalAuthoringTemplates.HingeCleanVersion,
            MechanicalAuthoringTemplates.HingeCleanTagCount,
            MechanicalAuthoringTemplates.HingeCleanLength);
        byte[] template = ValidateMechanicalTemplate(
            MechanicalAuthoringTemplates.HingeList,
            MechanicalAuthoringTemplates.HingeLength,
            MechanicalAuthoringTemplates.HingeVersion,
            MechanicalAuthoringTemplates.HingeTagCount);
        assembly.SetPendingStream(FindMatesListStreamPath(assembly), template);
    }

    /// <summary>
    /// Writes the measured cam-follower Cam template (CamTangent/CamCoincident
    /// plus the tutorial's other mates) onto the strict clean cam-follower base.
    /// </summary>
    internal static void AddTutorialCamMates(SldAssembly assembly)
    {
        ValidateMechanicalCleanBase(assembly, "moMateCamTangent", "cam-follower",
            MechanicalAuthoringTemplates.CamCleanVersion,
            MechanicalAuthoringTemplates.CamCleanTagCount,
            MechanicalAuthoringTemplates.CamCleanLength);
        byte[] template = ValidateMechanicalTemplate(
            MechanicalAuthoringTemplates.CamList,
            MechanicalAuthoringTemplates.CamLength,
            MechanicalAuthoringTemplates.CamVersion,
            MechanicalAuthoringTemplates.CamTagCount);
        assembly.SetPendingStream(FindMatesListStreamPath(assembly), template);
    }

    /// <summary>
    /// Writes the measured key pad PlanarAngle template (two PlanarAngle records
    /// plus the tutorial's other mates) onto the strict clean key pad base.
    /// </summary>
    internal static void AddTutorialPlanarAngleMates(SldAssembly assembly)
    {
        ValidateMechanicalCleanBase(assembly, "moMatePlanarAngleDim_c", "pad_1",
            MechanicalAuthoringTemplates.PlanarCleanVersion,
            MechanicalAuthoringTemplates.PlanarCleanTagCount,
            MechanicalAuthoringTemplates.PlanarCleanLength);
        byte[] template = ValidateMechanicalTemplate(
            MechanicalAuthoringTemplates.PlanarAngleList,
            MechanicalAuthoringTemplates.PlanarLength,
            MechanicalAuthoringTemplates.PlanarVersion,
            MechanicalAuthoringTemplates.PlanarTagCount);
        assembly.SetPendingStream(FindMatesListStreamPath(assembly), template);
    }

    private static void ValidateMechanicalCleanBase(
        SldAssembly assembly,
        string forbiddenClassName,
        string headerMarker,
        ushort cleanVersion,
        ushort cleanTagCount,
        int cleanLength)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (assembly.IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        string streamPath = FindMatesListStreamPath(assembly);
        byte[] current = assembly.GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        if (current.Length != cleanLength ||
            BitConverter.ToUInt16(current, 0) != cleanVersion ||
            BitConverter.ToUInt16(current, 4) != cleanTagCount)
            throw new NotSupportedException("The base is not the measured clean tutorial assembly.");
        if (MfcPrimitives.ScanMfcTags(current).Any(t => t.ClassName == forbiddenClassName) ||
            (forbiddenClassName == "moMateCamTangent" &&
             MfcPrimitives.ScanMfcTags(current).Any(t => t.ClassName == "moMateCamCoincident")))
            throw new NotSupportedException("The assembly already contains the target measured mate.");
        var headerStrings = ReadModelHeaderStrings(assembly);
        if (!headerStrings.Contains(headerMarker))
            throw new NotSupportedException("The measured tutorial assembly marker is not present in the target assembly tree.");
    }

    private static byte[] ValidateMechanicalTemplate(
        byte[] source, int expectedLength, ushort expectedVersion, ushort expectedTagCount)
    {
        byte[] template = (byte[])source.Clone();
        if (template.Length != expectedLength ||
            BitConverter.ToUInt16(template, 0) != expectedVersion ||
            BitConverter.ToUInt16(template, 4) != expectedTagCount)
            throw new InvalidOperationException("The embedded mechanical template header is not recognized.");
        return template;
    }

    private static int FindRackPinionReverseFlagOffset(byte[] data)
    {
        int result = -1;
        for (int i = 0; i + 8 <= data.Length; i++)
        {
            if (data[i] != 0xFF || data[i + 1] != 0xFE || data[i + 2] != 0xFF || data[i + 3] != 0x00)
                continue;
            if (data[i + 4] > 1 || data[i + 5] != 0x00 || data[i + 6] != 0x7A || data[i + 7] != 0x81)
                continue;
            if (result >= 0) return -1;
            result = i;
        }
        return result;
    }

    private static string FindMatesListStreamPath(SldAssembly assembly)
    {
        var keys = assembly.RawStreams.Keys
            .Where(k => k.EndsWith("-MatesList", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (keys.Length != 1)
            throw new NotSupportedException("Only a single-configuration MatesList stream is supported.");
        return keys[0];
    }

    private static void ValidateCleanBase(byte[] stream)
    {
        if (stream.Length != CleanHeaderLength)
            throw new NotSupportedException("Only a clean assembly without mates is supported.");
        if (BitConverter.ToUInt16(stream, 0) != ExpectedStreamVersion)
            throw new NotSupportedException($"Unsupported MatesList stream version 0x{BitConverter.ToUInt16(stream, 0):X4}.");
        if (BitConverter.ToUInt16(stream, 2) != 0)
            throw new NotSupportedException("Unsupported MatesList header layout.");
        if (BitConverter.ToUInt16(stream, 4) != 0)
            throw new NotSupportedException("The assembly already contains mate records.");
    }

    private readonly struct EntityParts
    {
        public EntityParts(string feature, string component, string assembly)
        {
            Feature = feature;
            Component = component;
            Assembly = assembly;
        }

        public string Feature { get; }
        public string Component { get; }
        public string Assembly { get; }
    }

    private static EntityParts ParseEntity(string entity, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(entity))
            throw new ArgumentException("A RefPlane selection name is required.", parameterName);
        var parts = entity.Split('@');
        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
            throw new NotSupportedException(
                $"Only RefPlane selection names of the form feature@component@assembly are supported (was '{entity}').");
        return new EntityParts(parts[0], parts[1], parts[2]);
    }

    private static void ValidateEntities(SldAssembly assembly, EntityParts first, EntityParts second)
    {
        if (string.Equals(first.Component, second.Component, StringComparison.Ordinal))
            throw new NotSupportedException("The two RefPlane entities must belong to different components.");
        if (!string.Equals(first.Assembly, second.Assembly, StringComparison.Ordinal))
            throw new NotSupportedException("The two RefPlane entities must belong to the same assembly instance.");
        if (string.Equals(first.Feature, second.Feature, StringComparison.Ordinal) &&
            string.Equals(first.Component, second.Component, StringComparison.Ordinal))
            throw new NotSupportedException("The two entity selection names must differ.");

        var headerStrings = ReadModelHeaderStrings(assembly);
        if (!headerStrings.Contains(first.Component) || !headerStrings.Contains(second.Component))
            throw new NotSupportedException("A selected component instance is not present in the target assembly tree.");
        if (!headerStrings.Contains(first.Assembly))
            throw new NotSupportedException("The selection names do not match the target assembly instance name.");
    }

    private static HashSet<string> ReadModelHeaderStrings(SldAssembly assembly)
    {
        var key = assembly.RawStreams.Keys.FirstOrDefault(k =>
            k.EndsWith("Config-0-ModelHeader", StringComparison.OrdinalIgnoreCase));
        if (key == null)
            throw new NotSupportedException("The target assembly has no Config-0-ModelHeader stream.");
        byte[] data = assembly.GetStreamBytes(key) ?? Array.Empty<byte>();

        var result = new HashSet<string>(StringComparer.Ordinal);
        int i = 0;
        while (i + 4 <= data.Length)
        {
            if (MfcPrimitives.IsWCharPrefix(data, i))
            {
                int cursor = i;
                string? value = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
                if (value != null && value.Length > 0)
                {
                    result.Add(value);
                    i = Math.Max(i, cursor - 1);
                }
            }
            i++;
        }
        return result;
    }

    private sealed class WCharField
    {
        public int Offset { get; set; }
        public int EndOffset { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private static byte[] RewriteEntityStrings(byte[] template, string entity1, string entity2)
    {
        var fields = new List<WCharField>();
        int i = 0;
        while (i + 4 <= template.Length)
        {
            if (MfcPrimitives.IsWCharPrefix(template, i))
            {
                int cursor = i;
                string? value = MfcPrimitives.ReadWChar(template, ref cursor, template.Length);
                if (value != null && value.Length > 0 && value.IndexOf('@') >= 0)
                {
                    fields.Add(new WCharField { Offset = i, EndOffset = cursor, Value = value });
                    i = Math.Max(i, cursor - 1);
                }
            }
            i++;
        }

        if (fields.Count != 4)
            throw new InvalidOperationException("The embedded mate template does not contain the four expected entity selections.");
        if (!IsComponentName(fields[0].Value) || !IsSelectionName(fields[1].Value, fields[0].Value) ||
            !IsComponentName(fields[2].Value) || !IsSelectionName(fields[3].Value, fields[2].Value))
            throw new InvalidOperationException("The embedded mate template entity layout is not recognized.");

        var entity1Parts = entity1.Split('@');
        var entity2Parts = entity2.Split('@');
        // The Coincident and Distance templates were serialized in different
        // native entity orders (Coincident: cube,cylinder; Distance: cylinder,cube).
        // When the caller's component names match the template placeholders in
        // reverse, assign them to the native slots so the written record stays
        // byte-identical to the natively verified v1 layout. Unknown component
        // names use the caller's order.
        string templateComponent0 = fields[0].Value.Split('@')[0];
        string templateComponent1 = fields[2].Value.Split('@')[0];
        string slotEntity1 = entity1;
        string slotEntity2 = entity2;
        if (string.Equals(entity1Parts[1], templateComponent1, StringComparison.Ordinal) &&
            string.Equals(entity2Parts[1], templateComponent0, StringComparison.Ordinal))
        {
            slotEntity1 = entity2;
            slotEntity2 = entity1;
        }

        var slot1Parts = slotEntity1.Split('@');
        var slot2Parts = slotEntity2.Split('@');
        string componentInstance1 = slot1Parts[1] + "@" + slot1Parts[2];
        string componentInstance2 = slot2Parts[1] + "@" + slot2Parts[2];
        var replacements = new[] { componentInstance1, slotEntity1, componentInstance2, slotEntity2 };

        var output = new List<byte>(template.Length + 64);
        int position = 0;
        for (int f = 0; f < fields.Count; f++)
        {
            var field = fields[f];
            output.AddRange(template.Skip(position).Take(field.Offset - position));
            output.AddRange(EncodeWChar(replacements[f]));
            position = field.EndOffset;
        }
        output.AddRange(template.Skip(position));
        return output.ToArray();
    }

    private static bool IsComponentName(string value)
    {
        return value.Length > 0 && value.IndexOf('@') > 0 &&
               value.IndexOf('@') == value.LastIndexOf('@') &&
               value.IndexOf('@') < value.Length - 1;
    }

    private static bool IsSelectionName(string value, string component)
    {
        if (value.Length == 0) return false;
        int first = value.IndexOf('@');
        if (first <= 0) return false;
        int second = value.IndexOf('@', first + 1);
        if (second <= first + 1 || value.LastIndexOf('@') != second) return false;
        return string.Equals(value.Substring(first + 1), component, StringComparison.Ordinal);
    }

    private static byte[] EncodeWChar(string value)
    {
        var bytes = new List<byte>(4 + value.Length * 2) { 0xFF, 0xFE, 0xFF };
        if (value.Length < 255)
        {
            bytes.Add((byte)value.Length);
        }
        else if (value.Length < ushort.MaxValue)
        {
            bytes.Add(0xFF);
            bytes.AddRange(BitConverter.GetBytes((ushort)value.Length));
        }
        else
        {
            bytes.Add(0xFF);
            bytes.Add(0xFF);
            bytes.AddRange(BitConverter.GetBytes((uint)value.Length));
        }
        bytes.AddRange(Encoding.Unicode.GetBytes(value));
        return bytes.ToArray();
    }

    /// <summary>
    /// Patches the strict D1 moLengthParameter_c record and the positive copies of
    /// its old value inside that parameter element. The measured run8 display
    /// dimension (moDisplayDistanceDim_c) carries no distance copy, so none is
    /// patched there.
    /// </summary>
    private static void PatchDistance(byte[] record, double distance)
    {
        byte[] pattern = { 0x06, 0x80, 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x31, 0x00 };
        int d1 = FindPattern(record, pattern);
        if (d1 < 0)
            throw new InvalidOperationException("The embedded Distance template has no D1 record.");
        if (FindPattern(record, pattern, d1 + 1) >= 0)
            throw new InvalidOperationException("The embedded Distance template has multiple D1 records.");
        if (!MateStructureDecoder.TryReadD1Record(record, d1, record.Length, out double oldValue))
            throw new InvalidOperationException("The embedded Distance template D1 record is malformed.");

        var tag = MfcPrimitives.ScanMfcTags(record)
            .FirstOrDefault(t => t.ClassName == "moLengthParameter_c" && t.DataStart == d1);
        int start = tag != null ? tag.DataStart : d1 + 32;
        int end = tag != null ? Math.Min(tag.DataEnd, record.Length) : record.Length;

        // Patch the D1 value and every positive copy of the old value inside the
        // parameter element (value/min/max/step metadata). Negative geometry
        // offsets (-oldValue) are deliberately left alone.
        for (int i = start; i + 8 <= end; i += 4)
        {
            double value = BitConverter.ToDouble(record, i);
            if (value == oldValue)
                BitConverter.GetBytes(distance).CopyTo(record, i);
        }
        // Ensure the authoritative D1 value is patched even if the parameter tag
        // was not found.
        BitConverter.GetBytes(distance).CopyTo(record, d1 + 32);
    }

    private static int FindPattern(byte[] data, byte[] pattern, int start = 0)
    {
        for (int i = Math.Max(0, start); i + pattern.Length <= data.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    // ── Advanced single-mate authoring (Task 6.2) ──────────────────────

    internal enum MateAuthoringEntityLayout
    {
        /// <summary>Four WChars: component/feature@component@assembly pair per entity.</summary>
        PlaneSelectionPairs = 0,

        /// <summary>Two component-instance WChars (Gear/Screw cylinder faces).</summary>
        ComponentInstances = 1,

        /// <summary>No entity WChars in the template (UniversalJoint specimen).</summary>
        None = 2
    }

    internal static void AddAdvancedMate(
        SldAssembly assembly,
        byte[] template,
        ushort streamVersion,
        string? modelHeaderMarker,
        MateAuthoringEntityLayout layout,
        string entity1,
        string entity2,
        double? parameter1,
        string? parameter1ElementClass,
        double? parameter2)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (template == null || template.Length == 0)
            throw new InvalidOperationException("The embedded mate template is empty.");
        if (assembly.IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        if (streamVersion != 0x00AB && streamVersion != 0x00BF)
            throw new NotSupportedException($"Unsupported template stream version 0x{streamVersion:X4}.");
        if (parameter1.HasValue) ValidatePositive(parameter1.Value, nameof(parameter1));
        if (parameter2.HasValue) ValidatePositive(parameter2.Value, nameof(parameter2));

        string streamPath = FindMatesListStreamPath(assembly);
        byte[] current = assembly.GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        ValidateCleanBase(current);

        var headerStrings = ReadModelHeaderStrings(assembly);
        if (!string.IsNullOrEmpty(modelHeaderMarker) && !headerStrings.Contains(modelHeaderMarker!))
            throw new NotSupportedException(
                $"The base does not carry the matching ModelHeader template (missing '{modelHeaderMarker}').");

        if (layout == MateAuthoringEntityLayout.PlaneSelectionPairs)
        {
            var first = ParseEntity(entity1, nameof(entity1));
            var second = ParseEntity(entity2, nameof(entity2));
            ValidateEntities(assembly, first, second);
        }
        else
        {
            var first = ParseComponentInstance(entity1, nameof(entity1));
            var second = ParseComponentInstance(entity2, nameof(entity2));
            ValidateComponentPair(headerStrings, first, second);
        }

        byte[] record;
        if (layout == MateAuthoringEntityLayout.PlaneSelectionPairs)
        {
            record = RewriteEntityStrings(template, entity1, entity2);
        }
        else if (layout == MateAuthoringEntityLayout.ComponentInstances)
        {
            record = RewriteComponentInstances(template, entity1, entity2);
        }
        else
        {
            record = (byte[])template.Clone();
        }

        if (parameter1.HasValue)
        {
            if (string.IsNullOrEmpty(parameter1ElementClass))
                throw new InvalidOperationException("A parameter element class is required for the selected mate template.");
            PatchNamedParameter(record, parameter1ElementClass!, "D1", parameter1.Value);
        }
        if (parameter2.HasValue)
            PatchNamedRecordAnywhere(record, "D2", parameter2.Value);

        byte[] header = (byte[])current.Clone();
        BitConverter.GetBytes(streamVersion).CopyTo(header, 0);
        BitConverter.GetBytes((ushort)0).CopyTo(header, 2);
        BitConverter.GetBytes((ushort)1).CopyTo(header, 4);
        byte[] updated = new byte[header.Length + record.Length];
        Buffer.BlockCopy(header, 0, updated, 0, header.Length);
        Buffer.BlockCopy(record, 0, updated, header.Length, record.Length);

        assembly.SetPendingStream(streamPath, updated);
    }

    private static void ValidatePositive(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 1_000_000)
            throw new ArgumentOutOfRangeException(parameterName, "The parameter must be a finite positive value.");
    }

    private readonly struct ComponentParts
    {
        public ComponentParts(string component, string assembly)
        {
            Component = component;
            Assembly = assembly;
        }

        public string Component { get; }
        public string Assembly { get; }
    }

    private static ComponentParts ParseComponentInstance(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A component instance name is required.", parameterName);
        var parts = value.Split('@');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            throw new NotSupportedException(
                $"Only component instance names of the form component@assembly are supported (was '{value}').");
        return new ComponentParts(parts[0], parts[1]);
    }

    private static void ValidateComponentPair(
        HashSet<string> headerStrings, ComponentParts first, ComponentParts second)
    {
        if (string.Equals(first.Component, second.Component, StringComparison.Ordinal))
            throw new NotSupportedException("The two entities must belong to different components.");
        if (!string.Equals(first.Assembly, second.Assembly, StringComparison.Ordinal))
            throw new NotSupportedException("The two entities must belong to the same assembly instance.");
        if (!headerStrings.Contains(first.Component) || !headerStrings.Contains(second.Component))
            throw new NotSupportedException("A selected component instance is not present in the target assembly tree.");
        if (!headerStrings.Contains(first.Assembly))
            throw new NotSupportedException("The selection names do not match the target assembly instance name.");
    }

    private static byte[] RewriteComponentInstances(byte[] template, string entity1, string entity2)
    {
        var fields = CollectWCharFields(template, requireAtCount: 1);
        if (fields.Count != 2)
            throw new InvalidOperationException("The embedded mate template does not contain the two expected component names.");
        if (!IsComponentName(fields[0].Value) || !IsComponentName(fields[1].Value))
            throw new InvalidOperationException("The embedded component-name layout is not recognized.");

        string slot1 = entity1;
        string slot2 = entity2;
        string template0 = fields[0].Value.Split('@')[0];
        string template1 = fields[1].Value.Split('@')[0];
        if (string.Equals(entity1.Split('@')[0], template1, StringComparison.Ordinal) &&
            string.Equals(entity2.Split('@')[0], template0, StringComparison.Ordinal))
        {
            slot1 = entity2;
            slot2 = entity1;
        }
        return RebuildWCharFields(template, fields, new[] { slot1, slot2 });
    }

    private static List<WCharField> CollectWCharFields(byte[] template, int requireAtCount)
    {
        var fields = new List<WCharField>();
        int i = 0;
        while (i + 4 <= template.Length)
        {
            if (MfcPrimitives.IsWCharPrefix(template, i))
            {
                int cursor = i;
                string? value = MfcPrimitives.ReadWChar(template, ref cursor, template.Length);
                if (value != null && value.Length > 0 &&
                    value.Count(c => c == '@') == requireAtCount)
                {
                    fields.Add(new WCharField { Offset = i, EndOffset = cursor, Value = value });
                    i = Math.Max(i, cursor - 1);
                }
            }
            i++;
        }
        return fields;
    }

    private static byte[] RebuildWCharFields(byte[] template, List<WCharField> fields, string[] replacements)
    {
        if (fields.Count != replacements.Length)
            throw new InvalidOperationException("Unexpected template WChar field count.");
        var output = new List<byte>(template.Length + 64);
        int position = 0;
        for (int f = 0; f < fields.Count; f++)
        {
            var field = fields[f];
            output.AddRange(template.Skip(position).Take(field.Offset - position));
            output.AddRange(EncodeWChar(replacements[f]));
            position = field.EndOffset;
        }
        output.AddRange(template.Skip(position));
        return output.ToArray();
    }

    /// <summary>
    /// Patches a strict D1 record inside exactly one tag of the given class and the
    /// positive copies of its old value inside that element.
    /// </summary>
    private static void PatchNamedParameter(byte[] record, string elementClassName, string name, double value)
    {
        var tags = MfcPrimitives.ScanMfcTags(record)
            .Where(t => t.ClassName == elementClassName)
            .ToList();
        if (tags.Count != 1)
            throw new InvalidOperationException($"The embedded template has {tags.Count} '{elementClassName}' elements.");
        int start = tags[0].DataStart;
        int end = Math.Min(tags[0].DataEnd, record.Length);
        if (!MateStructureDecoder.TryReadNamedRecord(record, start, end, name, out double oldValue))
            throw new InvalidOperationException($"The embedded template {elementClassName} {name} record is malformed.");
        for (int i = start; i + 8 <= end; i += 4)
        {
            if (BitConverter.ToDouble(record, i) == oldValue)
                BitConverter.GetBytes(value).CopyTo(record, i);
        }
        BitConverter.GetBytes(value).CopyTo(record, start + 32);
    }

    /// <summary>
    /// Patches one strict named record anywhere in the template (measured Gear D2
    /// record inside moFavoriteHandle_c).
    /// </summary>
    private static void PatchNamedRecordAnywhere(byte[] record, string name, double value)
    {
        int found = -1;
        int count = 0;
        for (int i = 0; i + 6 <= record.Length; i++)
        {
            if (record[i] != 0x06 || record[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(record, i + 2))
                continue;
            int cursor = i + 2;
            string? candidate = MfcPrimitives.ReadWChar(record, ref cursor, record.Length);
            if (!string.Equals(candidate, name, StringComparison.Ordinal)) continue;
            if (!MateStructureDecoder.TryReadNamedRecord(record, i, record.Length, name, out _)) continue;
            found = i;
            count++;
        }
        if (count != 1 || found < 0)
            throw new InvalidOperationException($"The embedded template has {count} strict {name} records.");
        BitConverter.GetBytes(value).CopyTo(record, found + 32);
    }
}
