using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fail-fast builder for the controlled chamfer authoring slices:
/// distance-distance (D1/D3) and angle-distance (D1 + D2 angle) on the measured single edge
/// of the ChamferOptions base body. Stream 0x70, parsed sketch chain, 0x67 marker and the exact
/// controlled base prefix are required; every other field is rejected before the stream changes.
/// </summary>
internal static class ChamferFeatureBuilder
{
    private const string SketchChainClass = "moSketchChain_c";
    private const int ChamferTypeAngleDistance = 1;
    private const int ChamferTypeDistanceDistance = 2;

    public static byte[] BuildChamferStream(
        byte[] currentStream,
        ChamferFeatureDefinitionData def,
        FeatureFolder sketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        bool angleDistance = ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (sketchFolder == null)
            throw new ArgumentNullException(nameof(sketchFolder));

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x70)
        {
            throw new NotSupportedException(
                $"Chamfer authoring is only supported for ResolvedFeatures streams version 0x70, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, ChamferSegments.ClassName) > 0)
        {
            throw new NotSupportedException(
                "Chamfer authoring currently supports only a stripped base body without an existing " +
                "Chamfer_c feature. No stream changes have been made.");
        }

        RequireSketchChain(currentStream, sketchFolder, sketchEndOffset, version);
        RequireSketchChildCountMarker(currentStream, version);
        RequireControlledBase(currentStream, version, angleDistance);

        byte[] featureBlock = MfcChamferSerializer.SerializeChamfer(def, featureName, angleDistance);
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    /// <summary>Validates the definition; returns true for angle-distance, false for distance-distance.</summary>
    internal static bool ValidateDefinition(ChamferFeatureDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (!def.EdgeChamferDistanceSide0.HasValue ||
            double.IsNaN(def.EdgeChamferDistanceSide0.Value) ||
            double.IsInfinity(def.EdgeChamferDistanceSide0.Value) ||
            def.EdgeChamferDistanceSide0.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.EdgeChamferDistanceSide0),
                $"Chamfer distance must be a positive finite number, but received {def.EdgeChamferDistanceSide0}.");
        }

        bool hasSide1 = def.EdgeChamferDistanceSide1.HasValue;
        bool hasAngle = def.EdgeChamferAngle.HasValue;
        if (hasSide1 == hasAngle)
        {
            throw new NotSupportedException(
                "Chamfer authoring requires exactly one of EdgeChamferDistanceSide1 (distance-distance) " +
                "or EdgeChamferAngle (angle-distance). No stream changes have been made.");
        }

        bool angleDistance = hasAngle;
        if (hasSide1)
        {
            double distance1 = def.EdgeChamferDistanceSide1!.Value;
            if (double.IsNaN(distance1) || double.IsInfinity(distance1) || distance1 <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(def.EdgeChamferDistanceSide1),
                    $"Chamfer side-1 distance must be a positive finite number, but received {distance1}.");
            }
            if (Math.Abs(distance1 - def.EdgeChamferDistanceSide0.Value) <= 1e-12)
            {
                throw new NotSupportedException(
                    "Chamfer authoring does not yet support equal distances (EqualDistance=true has no " +
                    "controlled sample). No stream changes have been made.");
            }
        }
        else
        {
            double angle = def.EdgeChamferAngle!.Value;
            if (double.IsNaN(angle) || double.IsInfinity(angle) || angle <= 0.0 || angle >= Math.PI / 2.0)
            {
                throw new ArgumentOutOfRangeException(nameof(def.EdgeChamferAngle),
                    $"Chamfer angle must be a finite value in (0, pi/2) radians, but received {angle}.");
            }
        }

        int expectedType = angleDistance ? ChamferTypeAngleDistance : ChamferTypeDistanceDistance;
        if (def.Type.HasValue && def.Type.Value != expectedType)
        {
            throw new NotSupportedException(
                $"Chamfer authoring expects Type={expectedType} for this mode, but received {def.Type.Value}. " +
                "No stream changes have been made.");
        }
        if (def.EqualDistance == true)
        {
            throw new NotSupportedException(
                "Chamfer authoring does not yet support EqualDistance=true. No stream changes have been made.");
        }

        // Only the fields bound by the measured layouts are authorable.
        foreach (var property in typeof(ChamferFeatureDefinitionData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (property.Name)
            {
                case nameof(ChamferFeatureDefinitionData.EdgeChamferDistanceSide0):
                case nameof(ChamferFeatureDefinitionData.EdgeChamferDistanceSide1):
                case nameof(ChamferFeatureDefinitionData.EdgeChamferAngle):
                case nameof(ChamferFeatureDefinitionData.Type):
                case nameof(ChamferFeatureDefinitionData.EqualDistance):
                case nameof(IFeatureData.ClassName):
                case nameof(IFeatureData.TypeId):
                case nameof(FeatureDefinitionData.ResolvedProperties):
                    continue;
            }

            object? value = property.GetValue(def);
            if (value != null)
            {
                throw new NotSupportedException(
                    $"Chamfer authoring does not yet support {property.Name}. No stream changes have been made.");
            }
        }

        return angleDistance;
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
                $"Cannot author a chamfer on the base body of sketch '{sketchName}' " +
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
            $"Cannot author a chamfer on the controlled base body (ResolvedFeatures version 0x{version:X2}): " +
            "the 0x67 sketch child-count marker was not found. No stream changes have been made.");
    }

    private static void RequireControlledBase(byte[] currentStream, ushort version, bool angleDistance)
    {
        byte[] normalized = (byte[])currentStream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }

        string sha = Sha256Hex(normalized);
        string expected = angleDistance
            ? ChamferSegments.BasePrefixSha256AngleDistance
            : ChamferSegments.BasePrefixSha256DistanceDistance;
        if (!string.Equals(sha, expected, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Chamfer authoring is limited to the controlled ChamferOptions base body for the " +
                $"{(angleDistance ? "angle-distance" : "distance-distance")} layout " +
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
