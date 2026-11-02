namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured SW2026 face-fillet layout (<c>Fillet_c</c>, FolderHeader TypeId 0x51, stream 0x70,
/// native ISimpleFilletFeatureData2.Type = 2). Two selected faces are serialized as two
/// moFaceBlend item/surface pairs and a two-block moCompFace_c; the radius is the D1 named
/// parameter in ThreeDRadiusDim_c plus its measured display copies.
///
/// Measured block (3190 bytes, fillet_face_3mm / fillet_face_5mm, source and resave):
///   Fillet_c 309 | moFaceBlendSurfIdRep_c 20 | moFaceBlendItemPair_c 0 |
///   moFaceBlendItem_c 354 | moCompFace_c 536 | (second pair) |
///   moDisplayRadialDim_c 665 | ThreeDRadiusDim_c 1147.
/// </summary>
internal static class FaceFilletSegments
{
    internal const string ClassName = "Fillet_c";
    internal const uint HeaderTypeId = 0x51u;
    internal const ushort StreamVersion = 0x70;
    internal const int NativeType = 2;

    internal const int BlockLength = 3190;
    internal const int HeaderRestLength = 263;
    internal const int BlendSurfIdRepLength = 20;
    internal const int BlendItemPairLength = 0;
    internal const int BlendItemLength = 354;
    internal const int CompFaceLength = 536;
    internal const int DisplayRadialDimLength = 665;
    internal const int ThreeDRadiusLength = 1147;

    internal const int CompFaceBlockCount = 2;
    internal const int CompFaceBlockLength = 268;
    internal const uint FaceCount = 6;
    internal const uint FaceClassWord = 0x300u;
    internal const uint BlendItemClassWord = 0x200u;
    internal const ushort SurfaceClass = 0x80E6;
    internal const ushort MappingClass = 0x80F1;
    internal const ushort BlendItemMarker = 0x80E6;
    internal const uint FirstFaceId = 0x5D2u;
    internal const uint SecondFaceId = 0x5CEu;
    internal const string ArchiveGuid = "7dc39425ad49b2547dc39425ad49b254";
    internal const string ArchiveIdHex = "54B249AD2594C37D54B249AD2594C37D";

    internal const int ThreeDRadiusParameterOffset = 6;
    internal const int ThreeDRadiusParameterValueOffset = 0x26;
    internal static readonly int[] PositiveRadiusSlots = { 0x26, 0x46, 0x4E, 0x5E, 0x66, 0xF6, 0x1E0, 0x1F8 };
    internal static readonly int[] NegativeRadiusSlots = { 0x93, 0x9B, 0x3B8, 0x3D0 };
    internal const int NegativeHalfRadiusSlot = 0xEE;

    internal const int ToleranceSlot1 = 0xD6;
    internal const int ToleranceSlot2 = 0x198;
    internal const int TinySlot1 = 0x3C0;
    internal const int TinySlot2 = 0x3D8;
    internal const double Tolerance3mm = 2.8912057932946783e-16;
    internal const double Tolerance5mm = 0.0;
    internal static readonly byte[] Tiny3mm = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x3C };
    internal static readonly byte[] Tiny5mm = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x3C };

    internal static bool IsMeasuredRadius(double radius) =>
        System.Math.Abs(radius - 0.003) <= 1e-12 || System.Math.Abs(radius - 0.005) <= 1e-12;

    internal static double ToleranceFor(double radius) =>
        System.Math.Abs(radius - 0.003) <= 1e-12 ? Tolerance3mm : Tolerance5mm;

    internal static byte[] TinyFor(double radius) =>
        System.Math.Abs(radius - 0.003) <= 1e-12 ? Tiny3mm : Tiny5mm;
}
