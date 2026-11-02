using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fail-fast builder for the first controlled fillet authoring slice:
/// constant radius on the measured single target edge of the fillet_2mm base body.
/// Requires stream version 0x70, a parsed sketch chain, the 0x67 sketch marker and the exact
/// controlled base-body prefix signature. Type, explicit edge selections and every other
/// unverified field are rejected before the stream is touched.
/// </summary>
internal static class FilletFeatureBuilder
{
    private const string SketchChainClass = "moSketchChain_c";

    // SHA-256 of the stripped fillet_2mm base prefix (bytes 0..Fillet_c tag) with the folder
    // count u16 at offset 4 normalized to zero. This pins the extruded cube and its target edge.
    private const string ExpectedBasePrefixSha256 =
        "63a536b5d8a3d364e0c3c1a115bb6464e820d5eb6d05ea012980fbcdfa3e0447";

    public static byte[] BuildFilletStream(
        byte[] currentStream,
        FilletFeatureDefinitionData def,
        FeatureFolder sketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (sketchFolder == null)
            throw new ArgumentNullException(nameof(sketchFolder));

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x70)
        {
            throw new NotSupportedException(
                $"Fillet authoring is only supported for ResolvedFeatures streams version 0x70, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, FilletSegments.ClassName) > 0)
        {
            throw new NotSupportedException(
                "Fillet authoring currently supports only a stripped base body without an existing " +
                "Fillet_c feature. No stream changes have been made.");
        }

        // The fillet is not a sketch child, so the 0x67 marker is required as a boundary sanity
        // check but is not updated. In the controlled fillet sample the marker sits before the
        // sketch folder, so search the base prefix rather than the extrude-style trailing window.
        RequireSketchChain(currentStream, sketchFolder, sketchEndOffset, version);
        RequireSketchChildCountMarker(currentStream, version);
        RequireControlledBase(currentStream, version);

        byte[] featureBlock = MfcFilletSerializer.SerializeFillet(def, featureName);
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    internal static void ValidateDefinition(FilletFeatureDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (!def.DefaultRadius.HasValue ||
            double.IsNaN(def.DefaultRadius.Value) || double.IsInfinity(def.DefaultRadius.Value) ||
            def.DefaultRadius.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.DefaultRadius),
                $"Fillet radius must be a positive finite number, but received {def.DefaultRadius}.");
        }

        // Only the radius is structurally verified. Type, edge selections, multi-radius and every
        // other API field remain unresolved and must not be silently accepted.
        foreach (var property in typeof(FilletFeatureDefinitionData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(FilletFeatureDefinitionData.DefaultRadius) ||
                property.Name == nameof(IFeatureData.ClassName) ||
                property.Name == nameof(IFeatureData.TypeId) ||
                property.Name == nameof(FeatureDefinitionData.ResolvedProperties))
                continue;

            object? value = property.GetValue(def);
            if (value != null)
            {
                throw new NotSupportedException(
                    $"Fillet authoring does not yet support {property.Name}. No stream changes have been made.");
            }
        }
    }

    private static void RequireSketchChain(
        byte[] currentStream, FeatureFolder sketchFolder, int sketchEndOffset, ushort version)
    {
        string sketchName = string.IsNullOrEmpty(sketchFolder.DisplayName)
            ? $"<tag {sketchFolder.TagOffset}>"
            : sketchFolder.DisplayName;
        int end = sketchEndOffset > sketchFolder.TagOffset
            ? Math.Min(sketchEndOffset, currentStream.Length)
            : currentStream.Length;
        bool hasChain = MfcPrimitives.ScanMfcTags(currentStream)
            .Any(t => t.ClassName == SketchChainClass &&
                      t.TagOffset >= sketchFolder.TagOffset &&
                      t.TagOffset < end);
        if (!hasChain)
        {
            throw new NotSupportedException(
                $"Cannot author a fillet on the base body of sketch '{sketchName}' " +
                $"(ResolvedFeatures version 0x{version:X2}): no parseable {SketchChainClass} was found. " +
                "No stream changes have been made.");
        }
    }

    private static void RequireSketchChildCountMarker(byte[] currentStream, ushort version)
    {
        for (int i = 0; i <= currentStream.Length - 4; i++)
        {
            if (currentStream[i] == 0x67 && currentStream[i + 1] == 0x00 &&
                currentStream[i + 2] == 0x00 && currentStream[i + 3] == 0x00)
                return;
        }

        throw new NotSupportedException(
            $"Cannot author a fillet on the controlled base body (ResolvedFeatures version 0x{version:X2}): " +
            "the 0x67 sketch child-count marker was not found. No stream changes have been made.");
    }

    private static void RequireControlledBase(byte[] currentStream, ushort version)
    {
        byte[] normalized = (byte[])currentStream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }

        string sha = Sha256Hex(normalized);
        if (!string.Equals(sha, ExpectedBasePrefixSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Fillet authoring is limited to the controlled fillet_2mm base body " +
                $"(ResolvedFeatures version 0x{version:X2}): base prefix signature {sha.Substring(0, 12)} " +
                "does not match the measured extruded cube/target edge. No stream changes have been made.");
        }
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
