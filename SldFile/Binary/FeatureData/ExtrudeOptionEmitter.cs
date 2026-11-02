using System;
using SldFile.Binary;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Emits the measured, structured MFC sub-objects that carry extrusion options which are not
/// part of the basic Boss/Blind payload:
/// <list type="bullet">
/// <item>BothDirections + DepthDirection2 (direction-2 indexed EndSpec / distance display stack).</item>
/// <item>DraftWhileExtruding + DraftAngle (moDisplayAngularDim_c -&gt; AngleDim_c -&gt; moAngleParameter_c).</item>
/// <item>FromType=3 + FromOffsetDistance (extended moFromEndSpec_c with a D19 length parameter).</item>
/// </list>
/// Every option scalar (D2, D3, D19) is written field by field through
/// <see cref="MfcStreamWriter"/>; the segment arrays in <see cref="ExtrudeSegments"/> only carry the
/// surrounding measured class payload/display metadata. Offsets and provenance are documented in
/// SldFileTests/TestFiles/Features2026/ExtrudeOptions/two-direction-evidence.md and OPTION-EVIDENCE.md.
/// </summary>
internal static class ExtrudeOptionEmitter
{
    // A named length parameter starts with u16 schema 0x8004, a WChar name, then this metadata
    // header: double 2.0, u32 FFFFFFFF, u32 0, empty WChar, u16 0. The scalar follows.
    private const ushort ParameterSchema = 0x8004;

    // moDisplayDistanceDim_c / moFeatureDimHandle_c are stored inside the favorite as indexed
    // references to the same payloads used by the primary direction.
    private const int DisplayDimTagLength = 28;
    private const int DisplayDimPayloadLength = 556;
    private const int FeatureDimHandleTagLength = 26;
    private const int FeatureDimHandlePayloadLength = 107;
    private const int DisplayBlockLength = DisplayDimTagLength + DisplayDimPayloadLength +
                                           FeatureDimHandleTagLength + FeatureDimHandlePayloadLength;

    private const int FavoritePrefixLength = 342;          // shared display metadata before option data
    private const int FavoriteRepeatPrefixLength = 436;    // direction-1 display state repeated by 0x8114
    private const int DraftFavoriteTruncatedLength = 452;  // bytes kept before the angular-dim enable slot

    // Indexed class references used by the version-0x70 favorite/fromEndSpec serialization.
    private const ushort SecondDirectionSpecReference = 0x810B;
    private const ushort FeatureDimHandleReference = 0x810D;
    private const ushort ParallelPlaneDimReference = 0x810F;
    private const ushort LengthParameterReference = 0x8111;
    private const ushort RepeatedDisplayReference = 0x8114;

    private static readonly byte[] FavoritePayload = ExtrudeSegments.NativeBossFavoritePayload;
    private static readonly byte[] DisplayDimPayload = Slice(
        ExtrudeSegments.BossDisplayDimBlock, DisplayDimTagLength, DisplayDimPayloadLength);
    private static readonly byte[] FeatureDimHandlePayload = Slice(
        ExtrudeSegments.BossDisplayDimBlock,
        DisplayBlockLength - FeatureDimHandlePayloadLength,
        FeatureDimHandlePayloadLength);
    private static readonly byte[] EmptyWChar = { 0xFF, 0xFE, 0xFF, 0x00 };

    private static byte[] Slice(byte[] source, int start, int count)
    {
        var result = new byte[count];
        Array.Copy(source, start, result, 0, count);
        return result;
    }

    // Measured persistent display markers. They identify the option dimension item inside the
    // favorite object; they are not geometric values and do not depend on the amplitude.
    private static readonly byte[] Direction2FavoriteMarker = { 0x04, 0x00, 0x00, 0x00 };
    private static readonly byte[] DraftFavoriteMarker = { 0x2D, 0x00, 0x5C, 0x00 };
    private static readonly byte[] OffsetFavoriteMarker = { 0x2D, 0x00, 0x5C, 0x00 };
    private static readonly byte[] Direction2FavoriteTailFlags = { 0x01, 0x00, 0x00, 0x01 };

    static ExtrudeOptionEmitter()
    {
        // Guard the measured segment layout. The emitted favorites/fromEndSpec records must stay
        // at the offsets the reader and SolidWorks expect (D2 at +1111, D19 at +679, ...).
        if (FavoritePayload.Length != 512 ||
            ExtrudeSegments.BossDisplayDimBlock.Length != DisplayBlockLength ||
            ExtrudeSegments.NativeBossFromEndSpecPayload.Length != 40 ||
            ExtrudeSegments.Direction2LengthParameterMid1.Length != 64 ||
            ExtrudeSegments.Direction2LengthParameterMid2.Length != 318 ||
            ExtrudeSegments.Direction2LengthParameterMid3.Length != 16 ||
            ExtrudeSegments.Direction2LengthParameterMid4.Length != 130 ||
            ExtrudeSegments.Direction2LengthParameterMid5.Length != 16 ||
            ExtrudeSegments.DraftDisplayAngularDimBlock.Length != 692 ||
            ExtrudeSegments.DraftAngleDimBlock.Length != 20 ||
            ExtrudeSegments.DraftAngleParameterMid1.Length != 530 ||
            ExtrudeSegments.DraftAngleParameterTail.Length != 511 ||
            ExtrudeSegments.StartOffsetLengthParameterMid1.Length != 64 ||
            ExtrudeSegments.StartOffsetLengthParameterMid2.Length != 157 ||
            ExtrudeSegments.StartOffsetLengthParameterMid3.Length != 105 ||
            ExtrudeSegments.StartOffsetLengthParameterMid4.Length != 16 ||
            ExtrudeSegments.StartOffsetLengthParameterMid5.Length != 178 ||
            ExtrudeSegments.StartOffsetLengthParameterMid6.Length != 16)
        {
            throw new InvalidOperationException(
                "Measured extrusion option segments do not have the expected layout. Review ExtrudeSegments.");
        }
    }

    /// <summary>
    /// Writes a moFavoriteHandle_c payload for a two-direction extrusion:
    /// shared prefix, direction-2 display marker, 0x810B second EndSpec reference and its zeroed
    /// Blind header, the mirrored display stack, and the D2 named length parameter.
    /// Produces exactly 2251 bytes.
    /// </summary>
    public static void WriteTwoDirectionFavorite(MfcStreamWriter writer, double depthDirection2)
    {
        writer.WriteMfcTag("moFavoriteHandle_c");
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoritePrefixLength));

        // 94-byte direction-2 favorite header: marker pair, zeroed Blind/no-reverse fields and an
        // empty WChar at +36. All three controlled amplitudes share this layout.
        writer.WriteBytes(Direction2FavoriteMarker);
        writer.WriteBytes(new byte[32]);
        writer.WriteBytes(EmptyWChar);
        writer.WriteBytes(new byte[54]);

        writer.WriteU16(SecondDirectionSpecReference);
        writer.WriteBytes(DisplayDimPayload);
        writer.WriteU16(FeatureDimHandleReference);
        writer.WriteBytes(FeatureDimHandlePayload);
        writer.WriteU16(ParallelPlaneDimReference);
        writer.WriteU16(2);
        writer.WriteU16(0);
        writer.WriteU16(LengthParameterReference);
        WriteDirection2LengthParameter(writer, depthDirection2);

        writer.WriteU16(RepeatedDisplayReference);
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoriteRepeatPrefixLength));

        // 74-byte tail flags of the direction-2 favorite record.
        writer.WriteBytes(Direction2FavoriteTailFlags);
        writer.WriteBytes(new byte[58]);
        writer.WriteU16(1);
        writer.WriteBytes(new byte[10]);
    }

    /// <summary>
    /// Writes the draft favorite payload followed by moDisplayAngularDim_c, AngleDim_c and
    /// moAngleParameter_c. The u32 enable flag lives 24 bytes before the display tag, as required
    /// by the native draft layout. Produces 476 bytes of favorite payload before the display block.
    /// </summary>
    public static void WriteDraftFavoriteAndDimension(MfcStreamWriter writer, double draftAngle)
    {
        writer.WriteMfcTag("moFavoriteHandle_c");
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoritePrefixLength));
        writer.WriteBytes(DraftFavoriteMarker);
        writer.WriteBytes(Slice(FavoritePayload, FavoritePrefixLength + DraftFavoriteMarker.Length, DraftFavoriteTruncatedLength - FavoritePrefixLength - DraftFavoriteMarker.Length));

        // Enable flag + 20 zero bytes immediately before moDisplayAngularDim_c.
        writer.WriteU32(1);
        writer.WriteBytes(new byte[20]);

        writer.WriteBytes(ExtrudeSegments.DraftDisplayAngularDimBlock);
        writer.WriteBytes(ExtrudeSegments.DraftAngleDimBlock);

        writer.WriteMfcTag("moAngleParameter_c");
        writer.WriteU16(ParameterSchema);
        writer.WriteWChar("D3");
        WriteNamedParameterHeader(writer);
        writer.WriteF64(draftAngle);
        writer.WriteBytes(ExtrudeSegments.DraftAngleParameterMid1);
        writer.WriteF64(draftAngle);
        writer.WriteBytes(ExtrudeSegments.DraftAngleParameterTail);
    }

    /// <summary>
    /// Writes the favorite payload used by an offset-start extrusion (the measured marker pair is
    /// the only difference from the forward Blind favorite).
    /// </summary>
    public static void WriteOffsetFavorite(MfcStreamWriter writer)
    {
        writer.WriteMfcTag("moFavoriteHandle_c");
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoritePrefixLength));
        writer.WriteBytes(OffsetFavoriteMarker);
        writer.WriteBytes(Slice(FavoritePayload, FavoritePrefixLength + OffsetFavoriteMarker.Length, FavoritePayload.Length - FavoritePrefixLength - OffsetFavoriteMarker.Length));
    }

    /// <summary>
    /// Writes the complete moFromEndSpec_c data for a start-offset extrusion: the internal
    /// discriminator 5 (API FromType=3), the indexed direction-1 display stack with a D19 named
    /// length parameter, the repeated display reference and the standard 40-byte tail.
    /// Produces exactly 1787 bytes.
    /// </summary>
    public static void WriteOffsetFromEndSpec(MfcStreamWriter writer, double fromOffsetDistance)
    {
        writer.WriteU32(5);
        writer.WriteU16(SecondDirectionSpecReference);
        writer.WriteBytes(DisplayDimPayload);
        writer.WriteU16(FeatureDimHandleReference);
        writer.WriteBytes(FeatureDimHandlePayload);
        writer.WriteU16(ParallelPlaneDimReference);
        writer.WriteU16(2);
        writer.WriteU16(0);
        writer.WriteU16(LengthParameterReference);
        WriteStartOffsetLengthParameter(writer, fromOffsetDistance);

        writer.WriteU16(RepeatedDisplayReference);
        writer.WriteBytes(Slice(FavoritePayload, 0, FavoriteRepeatPrefixLength));
        writer.WriteBytes(ExtrudeSegments.NativeBossFromEndSpecPayload);
    }

    private static void WriteDirection2LengthParameter(MfcStreamWriter writer, double value)
    {
        writer.WriteU16(ParameterSchema);
        writer.WriteWChar("D2");
        WriteNamedParameterHeader(writer);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid1);
        writer.WriteF64(-value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid2);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid3);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid4);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.Direction2LengthParameterMid5);
        writer.WriteF64(value);
        writer.WriteBytes(EmptyWChar);
    }

    private static void WriteStartOffsetLengthParameter(MfcStreamWriter writer, double value)
    {
        writer.WriteU16(ParameterSchema);
        writer.WriteWChar("D19");
        WriteNamedParameterHeader(writer);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid1);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid2);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid3);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid4);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid5);
        writer.WriteF64(value);
        writer.WriteBytes(ExtrudeSegments.StartOffsetLengthParameterMid6);
        writer.WriteF64(value);
        writer.WriteBytes(EmptyWChar);
    }

    private static void WriteNamedParameterHeader(MfcStreamWriter writer)
    {
        writer.WriteF64(2.0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
        writer.WriteBytes(EmptyWChar);
        writer.WriteU16(0);
    }
}
