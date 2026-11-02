using System;
using System.Linq;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fail-fast builder for single-direction Blind solid revolutions (moRevolution_c).
/// Supported scope: ResolvedFeatures stream version 0x70, the layout-matching extrusion-stripped
/// RevolveOptions base (forward 180 / reverse 180_reverse / full 360, pinned by a normalized base
/// signature), the controlled rectangle plus construction-axis sketch, a parsed moSketchChain_c,
/// and the 0x67 sketch child-count marker. The sketch/feature persistent serials are read from
/// moCompFeature_c and patched into the axis/sketch surface references. Everything else is
/// rejected before the stream is touched. Thin/both-directions/cut use separate complete measured
/// block templates in <see cref="RevolveOptionFeatureBuilder"/>; this builder handles the measured
/// single-direction solid serializer path.
/// </summary>
internal static class RevolveFeatureBuilder
{
    private const string SketchChainClass = "moSketchChain_c";
    private const string SketchLineClass = "sgLineHandle";

    // SHA-256 of the controlled sketch geometry (identical in all five RevolveOptions samples).
    // This pins the profile/axis that the measured topology and bounding-box formulas assume.
    private const string ExpectedLineSha256 =
        "9c1b69c5da6118966d652ba9fdc2d179e9e179bc475052984c195c6936f1bf18";

    // Controlled chain: four rectangle entities 2,3,4,5 (the construction axis is entity 1).
    private static readonly uint[] ExpectedChainEntityIds = { 2, 3, 4, 5 };

    // Normalized stripped-base signatures (folder count and 0x67 child value zeroed):
    // the forward/reverse/full layouts have different persistent serials and derived
    // caches, so each template may only be emitted on its own measured base.
    private const string ForwardBaseSha256 =
        "e11b5077364d5b5c926071c738912825980be0b23a957dba1124966dc49d88ae";
    private const string ReverseBaseSha256 =
        "b797b2a94b7645169b537a241f895f19530ec4ac797f7006d37c57669e9e3307";
    private const string FullBaseSha256 =
        "c97b7ee90f214ff1d6058388c9c64c2e7cf6fb2c97bf6536b0d44e6687aa3c7a";

    // moCompFeature_c serial pairs measured on the controlled bases.
    private const uint ForwardSerial = 0x6AE40CAD;
    private const uint ReverseSketchSerial = 0x6AE40CB0;
    private const uint ReverseFeatureSerial = 0x6AE40CB1;
    private const uint FullSerial = 0x6AE40CA3;
    private const ushort StrippedFolderCount = 19;

    private static readonly byte[] CompFeatureRecordMagic = { 0x38, 0x4A, 0x00, 0x00 };

    public static byte[] BuildRevolveStream(
        byte[] currentStream,
        RevolveFeatureDefinitionData def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));

        if (def.IsThinFeature || def.IsCut || def.BothDirections)
        {
            // Measured template blocks are appended to their exact stripped native base; the
            // existing solid serializer/base-signature path below is not used for these layouts.
            return RevolveOptionFeatureBuilder.Build(currentStream, def, featureName);
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x70)
        {
            throw new NotSupportedException(
                $"Revolution authoring is only supported for ResolvedFeatures streams version 0x70, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }

        VerifyControlledSketch(currentStream, targetSketchFolder, sketchEndOffset, version);
        int markerOffset = ExtrudeFeatureBuilder.FindSketchChildCountMarker(
            currentStream, targetSketchFolder, sketchEndOffset);

        bool full = MfcRevolveSerializer.IsFullRevolution(def.Angle);
        string expectedBaseSha = full
            ? FullBaseSha256
            : def.ReverseDirection ? ReverseBaseSha256 : ForwardBaseSha256;
        string baseName = full ? "revolve_360" : def.ReverseDirection ? "revolve_180_reverse" : "revolve_180";
        string baseSha = ComputeControlledBaseSha(currentStream, markerOffset);
        if (!string.Equals(baseSha, expectedBaseSha, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Revolution authoring for this layout requires the controlled extrusion-stripped " +
                $"{baseName} base (normalized base signature {baseSha.Substring(0, 12)} does not match). " +
                "The forward, reverse and full layout templates carry different persistent sketch/feature " +
                "serials and derived caches; changing the base would produce a non-rebuildable feature. " +
                "No stream changes have been made.");
        }

        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        if (folderCount != StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Revolution authoring requires the extrusion-stripped base with folder count " +
                $"{StrippedFolderCount}, but the document uses {folderCount}. No stream changes have been made.");
        }
        uint childCount = BitConverter.ToUInt32(currentStream, markerOffset + 4);
        if (childCount != 0)
        {
            throw new NotSupportedException(
                $"Revolution authoring requires the extrusion-stripped base with a zero 0x67 sketch child " +
                $"count, but the document uses {childCount}. No stream changes have been made.");
        }

        var (sketchSerial, featureSerial) = ReadCompFeatureSerials(currentStream);
        ValidateControlledSerials(sketchSerial, featureSerial, full, def.ReverseDirection);

        int existingCount = ExtrudeFeatureBuilder.CountOccurrences(currentStream, "moRevolution_c");
        uint featureId = 0x6DE654A5 + (uint)existingCount;

        byte[] featureBlock = MfcRevolveSerializer.SerializeRevolve(
            def, featureName, sketchSerial, featureSerial, featureId);

        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);

        BitConverter.GetBytes(childCount + 1).CopyTo(newStream, markerOffset + 4);

        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    internal static void ValidateDefinition(RevolveFeatureDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (double.IsNaN(def.Angle) || double.IsInfinity(def.Angle) ||
            def.Angle <= 0.0 || def.Angle > MfcRevolveSerializer.FullRevolution + 1e-12)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Angle),
                $"Revolution angle must be a finite value in (0, 2*pi] radians, but received {def.Angle}.");
        }

        if (def.IsThinFeature)
        {
            ValidateThinTemplateDefinition(def);
            return;
        }
        if (def.IsCut)
        {
            ValidateCutTemplateDefinition(def);
            return;
        }
        if (def.BothDirections)
        {
            ValidateBothTemplateDefinition(def);
            return;
        }

        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.Type != 0, nameof(def.Type));
        RejectUnsupported(def.AngleDirection2 != 0, nameof(def.AngleDirection2));
        RejectUnsupported(def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.Thickness1 != 0, nameof(def.Thickness1));
        RejectUnsupported(def.Thickness2 != 0, nameof(def.Thickness2));
        RejectUnsupported(!def.Merge, nameof(def.Merge));

        bool full = MfcRevolveSerializer.IsFullRevolution(def.Angle);
        // The controlled partial samples are 90 and 180 degrees; a partial sweep greater than
        // 180 degrees has no measured topology (face/selection structure) and is not guessed.
        RejectUnsupported(!full && def.Angle > Math.PI, nameof(def.Angle));

        // A reversed full revolution has no controlled sample; the measured reverse layout is partial only.
        RejectUnsupported(def.ReverseDirection && full, nameof(def.ReverseDirection));
    }

    private static void ValidateThinTemplateDefinition(RevolveFeatureDefinitionData def)
    {
        RejectUnsupported(def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.Type != 0, nameof(def.Type));
        RejectUnsupported(def.AngleDirection2 != 0, nameof(def.AngleDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.Thickness2 != 0, nameof(def.Thickness2));
        RejectUnsupported(!IsPositiveFinite(def.Thickness1), nameof(def.Thickness1));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        // Only the two measured thin templates exist; arbitrary angles have no parameterized
        // display/cache evidence.
        if (Math.Abs(def.Angle - Math.PI / 2.0) > 1e-12 &&
            Math.Abs(def.Angle - 2.0 * Math.PI) > 1e-12)
        {
            throw new NotSupportedException(
                "Thin revolution authoring supports only the measured 90-degree and 360-degree " +
                "templates. No stream changes have been made.");
        }
    }

    private static void ValidateCutTemplateDefinition(RevolveFeatureDefinitionData def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.Type != 0, nameof(def.Type));
        RejectUnsupported(def.AngleDirection2 != 0, nameof(def.AngleDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.Thickness1 != 0, nameof(def.Thickness1));
        RejectUnsupported(def.Thickness2 != 0, nameof(def.Thickness2));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        if (Math.Abs(def.Angle - 2.0 * Math.PI) > 1e-12)
        {
            throw new NotSupportedException(
                "Cut revolution authoring supports only the measured full 360-degree template " +
                "(the native 90-degree cut creation returned NULL). No stream changes have been made.");
        }
    }

    private static void ValidateBothTemplateDefinition(RevolveFeatureDefinitionData def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.Type != 2, nameof(def.Type));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.Thickness1 != 0, nameof(def.Thickness1));
        RejectUnsupported(def.Thickness2 != 0, nameof(def.Thickness2));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        if (!IsPositiveFinite(def.Angle) || def.Angle > 2.0 * Math.PI ||
            !IsPositiveFinite(def.AngleDirection2) || def.AngleDirection2 > 2.0 * Math.PI)
        {
            throw new NotSupportedException(
                $"Both-direction revolution {nameof(def.AngleDirection2)} angles must be finite values " +
                "in (0, 2*pi]. No stream changes have been made.");
        }
    }

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private static void VerifyControlledSketch(
        byte[] currentStream, FeatureFolder targetSketchFolder, int sketchEndOffset, ushort version)
    {
        string sketchName = string.IsNullOrEmpty(targetSketchFolder.DisplayName)
            ? $"<tag {targetSketchFolder.TagOffset}>"
            : targetSketchFolder.DisplayName;

        var chain = FindSketchElement(currentStream, targetSketchFolder, sketchEndOffset, SketchChainClass);
        var line = FindSketchElement(currentStream, targetSketchFolder, sketchEndOffset, SketchLineClass);
        if (chain == null || line == null)
        {
            throw new NotSupportedException(
                $"Cannot author a revolution on sketch '{sketchName}' (ResolvedFeatures version 0x{version:X2}): " +
                $"the sketch must expose a parsed {SketchChainClass} and {SketchLineClass}. " +
                "Revolution authoring is currently limited to the controlled rectangle + construction-axis sketch. " +
                "No stream changes have been made.");
        }

        string lineSha = Sha256Hex(currentStream, line);
        bool chainMatches = ChainMatchesControlledProfile(currentStream, chain);
        if (!chainMatches || !string.Equals(lineSha, ExpectedLineSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cannot author a revolution on sketch '{sketchName}' (ResolvedFeatures version 0x{version:X2}): " +
                "the sketch geometry does not match the controlled rectangle + construction-axis specimen " +
                $"(chain {(chainMatches ? "ids-match" : "ids-differ")}, lines {lineSha.Substring(0, 12)}). " +
                "The measured revolution topology and bounding box assume that exact profile/axis. " +
                "No stream changes have been made.");
        }
    }

    private static bool ChainMatchesControlledProfile(byte[] currentStream, MfcTag chain)
    {
        int length = chain.DataEnd - chain.DataStart;
        if (length < 2 || chain.DataStart < 0 || chain.DataStart + length > currentStream.Length)
            return false;

        ushort count = BitConverter.ToUInt16(currentStream, chain.DataStart);
        if (count != ExpectedChainEntityIds.Length || length < 2 + 4 * count)
            return false;

        for (int i = 0; i < count; i++)
        {
            uint id = BitConverter.ToUInt32(currentStream, chain.DataStart + 2 + 4 * i);
            if (id != ExpectedChainEntityIds[i])
                return false;
        }
        return true;
    }

    private static MfcTag? FindSketchElement(
        byte[] currentStream, FeatureFolder targetSketchFolder, int sketchEndOffset, string className)
    {
        int end = sketchEndOffset > targetSketchFolder.TagOffset
            ? Math.Min(sketchEndOffset, currentStream.Length)
            : currentStream.Length;
        return MfcPrimitives.ScanMfcTags(currentStream)
            .FirstOrDefault(t => t.ClassName == className &&
                                 t.TagOffset >= targetSketchFolder.TagOffset &&
                                 t.TagOffset < end);
    }

    private static string Sha256Hex(byte[] data, MfcTag tag)
    {
        int start = tag.DataStart;
        int count = Math.Max(0, tag.DataEnd - start);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data, start, count);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Hashes the extrusion-stripped base with the folder count and the 0x67 sketch
    /// child-count value normalized to zero. This pins the exact measured RevolveOptions
    /// base including its persistent sketch/feature serials and derived caches.
    /// </summary>
    private static string ComputeControlledBaseSha(byte[] stream, int markerOffset)
    {
        byte[] normalized = (byte[])stream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }
        if (markerOffset >= 0 && markerOffset + 8 <= normalized.Length)
            Array.Clear(normalized, markerOffset + 4, 4);

        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Reads the sketch and consuming-feature persistent serials from the controlled
    /// moCompFeature_c records (first record id 0x49 = sketch, second id 0x4E = feature).
    /// The reverse control uses distinct sketch/feature serials, so both must be patched
    /// independently in the emitted surface-reference blocks.
    /// </summary>
    private static (uint SketchSerial, uint FeatureSerial) ReadCompFeatureSerials(byte[] stream)
    {
        var tag = MfcPrimitives.ScanMfcTags(stream)
            .FirstOrDefault(t => t.ClassName == "moCompFeature_c");
        if (tag == null)
        {
            throw new NotSupportedException(
                "The controlled revolve base does not expose a moCompFeature_c sketch/feature reference record. " +
                "No stream changes have been made.");
        }

        int start = tag.DataStart;
        int limit = Math.Min(start + 512, stream.Length);
        int first = FindCompFeatureRecord(stream, start, limit);
        if (first < 0 || first + 12 > limit)
        {
            throw new NotSupportedException(
                "The controlled revolve base has no readable sketch serial record in moCompFeature_c. " +
                "No stream changes have been made.");
        }
        int second = FindCompFeatureRecord(stream, first + 12, limit);
        if (second < 0 || second + 12 > limit)
        {
            throw new NotSupportedException(
                "The controlled revolve base has no readable feature serial record in moCompFeature_c. " +
                "No stream changes have been made.");
        }

        uint sketchId = BitConverter.ToUInt32(stream, first + 4);
        uint sketchSerial = BitConverter.ToUInt32(stream, first + 8);
        uint featureId = BitConverter.ToUInt32(stream, second + 4);
        uint featureSerial = BitConverter.ToUInt32(stream, second + 8);
        if (sketchId != 0x49 || featureId != 0x4E || sketchSerial == 0 || featureSerial == 0)
        {
            throw new NotSupportedException(
                "The moCompFeature_c records of the controlled revolve base do not match the measured " +
                "sketch/feature serial layout. No stream changes have been made.");
        }
        return (sketchSerial, featureSerial);
    }

    private static int FindCompFeatureRecord(byte[] stream, int start, int limit)
    {
        int found = -1;
        for (int i = Math.Max(0, start); i <= limit - CompFeatureRecordMagic.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < CompFeatureRecordMagic.Length; j++)
            {
                if (stream[i + j] != CompFeatureRecordMagic[j]) { match = false; break; }
            }
            if (match)
            {
                found = i;
                break;
            }
        }
        return found;
    }

    private static void ValidateControlledSerials(
        uint sketchSerial, uint featureSerial, bool full, bool reverse)
    {
        uint expectedSketch = full ? FullSerial : reverse ? ReverseSketchSerial : ForwardSerial;
        uint expectedFeature = full ? FullSerial : reverse ? ReverseFeatureSerial : ForwardSerial;
        if (sketchSerial != expectedSketch || featureSerial != expectedFeature)
        {
            string layout = full ? "full 2*pi" : reverse ? "reverse partial" : "forward partial";
            throw new NotSupportedException(
                $"The controlled revolve base serials (sketch 0x{sketchSerial:X8}, feature 0x{featureSerial:X8}) " +
                $"do not match the measured {layout} layout (expected 0x{expectedSketch:X8}/0x{expectedFeature:X8}). " +
                "No stream changes have been made.");
        }
    }

    private static void RejectUnsupported(bool unsupported, string property)
    {
        if (unsupported)
            throw new NotSupportedException(
                $"Revolution authoring does not yet support {property}. No stream changes have been made.");
    }
}
