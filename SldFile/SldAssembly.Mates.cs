using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFile;

public partial class SldAssembly
{
    private const ushort ExpectedStreamVersion = 0x00AB;
    private const int CleanHeaderLength = 6;

    private static readonly byte[] DualCoincidentDistanceList = MateAuthoringSegments.DualCoincidentDistanceList;

    private const int WidthFlipFalseLength = 39022;
    private const int WidthFlipTrueLength = 39046;
    private const ushort WidthStreamVersion = 0x0102;
    private static readonly byte[] WidthFlipFalseList = MateAuthoringSegments.WidthFlipFalseList;
    private static readonly byte[] WidthFlipTrueList = MateAuthoringSegments.WidthFlipTrueList;

    private const int RackPinionFullListLength = 24409;
    private const ushort RackPinionStreamVersion = 0x00AE;
    private static readonly byte[] RackPinionFullList = MateAuthoringSegments.RackPinionFullList;

    private const int PathFullListLength = 72440;
    private const int PathCleanBaseLength = 48983;
    private const ushort PathStreamVersion = 0x00E6;
    private const ushort PathFullListTagCount = 16;
    private const ushort PathCleanBaseTagCount = 12;
    private static readonly byte[] PathFullList = MateAuthoringSegments.PathFullList;

    private const int HingeLength = 24795;
    private const ushort HingeVersion = 0x00E7;
    private const ushort HingeTagCount = 4;
    private const int HingeCleanLength = 6;
    private const ushort HingeCleanVersion = 0x00E7;
    private const ushort HingeCleanTagCount = 0;
    private static readonly byte[] HingeList = MateAuthoringSegments.HingeList;

    private const int CamLength = 24081;
    private const ushort CamVersion = 0x00B1;
    private const ushort CamTagCount = 9;
    private const int CamCleanLength = 16435;
    private const ushort CamCleanVersion = 0x00B1;
    private const ushort CamCleanTagCount = 7;
    private static readonly byte[] CamList = MateAuthoringSegments.CamList;

    private const int PlanarLength = 40820;
    private const ushort PlanarVersion = 0x0186;
    private const ushort PlanarTagCount = 9;
    private const int PlanarCleanLength = 32962;
    private const ushort PlanarCleanVersion = 0x0186;
    private const ushort PlanarCleanTagCount = 7;
    private static readonly byte[] PlanarAngleList = MateAuthoringSegments.PlanarAngleList;

    public void AddCoincidentDistanceMates(
        string coincidentEntity1,
        string coincidentEntity2,
        string distanceEntity1,
        string distanceEntity2,
        double distance)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        if (double.IsNaN(distance) || double.IsInfinity(distance) || distance <= 0 || distance > 1000)
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be a finite value in (0, 1000] meters.");

        byte[] template = DualCoincidentDistanceList;
        if (template == null || template.Length <= CleanHeaderLength)
            throw new InvalidOperationException("The embedded dual-mate template is empty.");
        if (BitConverter.ToUInt16(template, 0) != ExpectedStreamVersion ||
            BitConverter.ToUInt16(template, 2) != 0 ||
            BitConverter.ToUInt16(template, 4) != 2)
            throw new InvalidOperationException("The embedded dual-mate template header is not recognized.");

        string streamPath = FindMatesListStreamPath();
        byte[] current = GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        ValidateCleanBase();

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

        var headerStrings = ReadModelHeaderStrings();
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

        var coincidentMate = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident)
        {
            DisplayName = "Coincident1",
            Alignment = AssemblyMateAlignment.Aligned
        };
        coincidentMate.AddPlaneReference(coincidentEntity1);
        coincidentMate.AddPlaneReference(coincidentEntity2);

        var distanceMate = new StandardMateModel("moMateDistanceDim_c", AssemblyMateType.Distance)
        {
            DisplayName = "Distance1",
            Alignment = AssemblyMateAlignment.Aligned,
            Distance = distance
        };
        distanceMate.AddPlaneReference(distanceEntity1);
        distanceMate.AddPlaneReference(distanceEntity2);

        MatesList.Mates.Add(coincidentMate);
        MatesList.Mates.Add(distanceMate);

        SetPendingStream(streamPath, updated);
    }

    public void AddCoincidentDistanceMates(
        string coincidentEntity1,
        string coincidentEntity2,
        string distanceEntity1,
        string distanceEntity2)
        => AddCoincidentDistanceMates(coincidentEntity1, coincidentEntity2, distanceEntity1, distanceEntity2, 0.01);

    public void SetWidthMateDimension(double distance, bool flipDimension)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        if (!(Math.Abs(distance - 0.01) < 1e-12 || Math.Abs(distance - 0.02) < 1e-12))
            throw new ArgumentOutOfRangeException(nameof(distance),
                "Only the measured 0.01 m and 0.02 m width dimensions are supported.");

        string streamPath = FindMatesListStreamPath();
        byte[] current = GetStreamBytes(streamPath)
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
            ? (byte[])WidthFlipTrueList.Clone()
            : (byte[])WidthFlipFalseList.Clone();
        if (template.Length != (flipDimension
                ? WidthFlipTrueLength
                : WidthFlipFalseLength) ||
            BitConverter.ToUInt16(template, 0) != WidthStreamVersion)
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
        MfcParameterReader.PatchParameterValueAndCopies(template, start, nextTag, start + 32, oldValue, distance);

        var widthMate = MatesList.Mates.OfType<WidthMateModel>().FirstOrDefault();
        if (widthMate != null)
        {
            widthMate.ConstraintType = AssemblyMateWidthConstraint.Dimension;
            widthMate.DistanceFromEnd = distance;
            widthMate.FlipDimension = flipDimension;
        }

        SetPendingStream(streamPath, template);
    }

    public void AddRackPinionMate(
        string rackComponent,
        string pinionComponent,
        AssemblyMateRackPinionDiameterType diameterType,
        double diameterValue,
        bool reverse)
    {
        if (diameterType != AssemblyMateRackPinionDiameterType.PinionPitchDiameter &&
            diameterType != AssemblyMateRackPinionDiameterType.RackTravelPerRevolution)
            throw new ArgumentOutOfRangeException(nameof(diameterType), "A known RackPinion diameter option is required.");
        AddRackPinionMate(
            rackComponent, pinionComponent,
            diameterType == AssemblyMateRackPinionDiameterType.PinionPitchDiameter ? 0 : 1,
            diameterValue, reverse);
    }

    public void AddRackPinionMate(
        string rackComponent,
        string pinionComponent,
        int diameterTypeCode,
        double diameterValue,
        bool reverse)
    {
        if (IsReadOnly)
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

        string streamPath = FindMatesListStreamPath();
        byte[] current = GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        if (current.Length < 6 ||
            BitConverter.ToUInt16(current, 0) != RackPinionStreamVersion ||
            BitConverter.ToUInt16(current, 4) != 6)
            throw new NotSupportedException("The base is not the measured clean three-mate rack-and-pinion assembly.");
        var currentTags = MfcPrimitives.ScanMfcTags(current);
        if (currentTags.Any(t => t.ClassName == "moMateRackPinionDim_c"))
            throw new NotSupportedException("The assembly already contains a RackPinion mate.");
        if (!currentTags.Any(t => t.ClassName == "moMateCoincident") ||
            !currentTags.Any(t => t.ClassName == "moMateDistanceDim_c") ||
            !currentTags.Any(t => t.ClassName == "moMateParallel"))
            throw new NotSupportedException("The base does not carry the measured three-mate rack-and-pinion layout.");

        byte[] template = (byte[])RackPinionFullList.Clone();
        if (template.Length != RackPinionFullListLength ||
            BitConverter.ToUInt16(template, 0) != RackPinionStreamVersion ||
            BitConverter.ToUInt16(template, 4) != 7)
            throw new InvalidOperationException("The embedded RackPinion template header is not recognized.");

        var headerStrings = ReadModelHeaderStrings();
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
        MfcParameterReader.PatchParameterValueAndCopies(template, d1, d1End, d1 + 32, oldD1, newD1);

        BitConverter.GetBytes((uint)diameterTypeCode).CopyTo(template, template.Length - 4);
        int reverseFlag = FindRackPinionReverseFlagOffset(template);
        if (reverseFlag < 0)
            throw new InvalidOperationException("The embedded RackPinion template reverse flag is not unique.");
        template[reverseFlag + 4] = reverse ? (byte)0 : (byte)1;

        var rackMate = new RackPinionMateModel
        {
            DisplayName = "RackPinion1",
            Alignment = AssemblyMateAlignment.Aligned,
            DiameterType = diameterTypeCode == 0
                ? AssemblyMateRackPinionDiameterType.PinionPitchDiameter
                : AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
            DiameterValue = diameterValue,
            Reverse = reverse
        };
        rackMate.SetEntities(rackComponent, pinionComponent);
        MatesList.Mates.Add(rackMate);

        SetPendingStream(streamPath, template);
    }

    public void AddTutorialPathMates()
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        string streamPath = FindMatesListStreamPath();
        byte[] current = GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        if (current.Length != PathCleanBaseLength ||
            BitConverter.ToUInt16(current, 0) != PathStreamVersion ||
            BitConverter.ToUInt16(current, 4) != PathCleanBaseTagCount)
            throw new NotSupportedException("The base is not the measured clean motion_path_tutorial assembly.");
        var tags = MfcPrimitives.ScanMfcTags(current);
        if (tags.Any(t => t.ClassName == "moMatePath_c"))
            throw new NotSupportedException("The assembly already contains a Path mate.");
        var headerStrings = ReadModelHeaderStrings();
        if (!headerStrings.Contains("chainpath-1") || !headerStrings.Contains("motion_path_tutorial"))
            throw new NotSupportedException("The measured chainpath/motion_path_tutorial components are not present in the target assembly tree.");

        byte[] template = (byte[])PathFullList.Clone();
        if (template.Length != PathFullListLength ||
            BitConverter.ToUInt16(template, 0) != PathStreamVersion ||
            BitConverter.ToUInt16(template, 4) != PathFullListTagCount)
            throw new InvalidOperationException("The embedded Path template header is not recognized.");
        SetPendingStream(streamPath, template);
    }

    public void AddTutorialHingeMates()
    {
        ValidateMechanicalCleanBase("moMateHinge", "chain_link1",
            HingeCleanVersion,
            HingeCleanTagCount,
            HingeCleanLength);
        byte[] template = ValidateMechanicalTemplate(
            HingeList,
            HingeLength,
            HingeVersion,
            HingeTagCount);
        SetPendingStream(FindMatesListStreamPath(), template);
    }

    public void AddTutorialCamMates()
    {
        ValidateMechanicalCleanBase("moMateCamTangent", "cam-follower",
            CamCleanVersion,
            CamCleanTagCount,
            CamCleanLength);
        byte[] template = ValidateMechanicalTemplate(
            CamList,
            CamLength,
            CamVersion,
            CamTagCount);
        SetPendingStream(FindMatesListStreamPath(), template);
    }

    public void AddTutorialPlanarAngleMates()
    {
        ValidateMechanicalCleanBase("moMatePlanarAngleDim_c", "pad_1",
            PlanarCleanVersion,
            PlanarCleanTagCount,
            PlanarCleanLength);
        byte[] template = ValidateMechanicalTemplate(
            PlanarAngleList,
            PlanarLength,
            PlanarVersion,
            PlanarTagCount);
        SetPendingStream(FindMatesListStreamPath(), template);
    }

    private void ValidateMechanicalCleanBase(
        string forbiddenClassName,
        string headerMarker,
        ushort cleanVersion,
        ushort cleanTagCount,
        int cleanLength)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        string streamPath = FindMatesListStreamPath();
        byte[] current = GetStreamBytes(streamPath)
            ?? throw new NotSupportedException("The assembly has no MatesList stream.");
        if (current.Length != cleanLength ||
            BitConverter.ToUInt16(current, 0) != cleanVersion ||
            BitConverter.ToUInt16(current, 4) != cleanTagCount)
            throw new NotSupportedException("The base is not the measured clean tutorial assembly.");
        if (MfcPrimitives.ScanMfcTags(current).Any(t => t.ClassName == forbiddenClassName) ||
            (forbiddenClassName == "moMateCamTangent" &&
             MfcPrimitives.ScanMfcTags(current).Any(t => t.ClassName == "moMateCamCoincident")))
            throw new NotSupportedException("The assembly already contains the target measured mate.");
        var headerStrings = ReadModelHeaderStrings();
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

    private static List<MfcPrimitives.WCharField> ReadAtFields(byte[] data) =>
        MfcPrimitives.ScanWCharFields(data, 0, data.Length, predicate: static s => s.IndexOf('@') >= 0);

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

    private static bool IsComponentName(string s) => s.Count(c => c == '@') == 1;
    private static bool IsSelectionName(string s, string comp) => s.EndsWith("@" + comp, StringComparison.Ordinal) && s.Count(c => c == '@') == 2;

    private static void ValidateGroupPair(
        (string Feature, string Component, string Assembly) first,
        (string Feature, string Component, string Assembly) second,
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

    private static ((string Feature, string Component, string Assembly) First, (string Feature, string Component, string Assembly) Second) OrderPairByTemplate(
        (string Feature, string Component, string Assembly) first,
        (string Feature, string Component, string Assembly) second,
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

    private static byte[] RebuildAtFields(byte[] template, List<MfcPrimitives.WCharField> fields, string[] replacements)
    {
        if (replacements.Length != fields.Count)
            throw new InvalidOperationException("The dual-mate template replacement count does not match its entity fields.");
        return MfcPrimitives.RewriteWCharFields(template, fields, replacements);
    }

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

        MfcParameterReader.PatchParameterValueAndCopies(record, start, end, d1 + 32, oldValue, distance);
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
}
