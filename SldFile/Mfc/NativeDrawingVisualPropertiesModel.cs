using System;

namespace SldFile.Mfc;

// moVisualProperties_c::Serialize 0x3CBC8580 (sldmfcu.dll).
internal sealed class NativeDrawingVisualPropertiesModel : IMfcSerializable
{
    internal uint BackgroundColor, EdgeColor, FaceColor;
    internal NativeDrawingMaterialModel Material = new NativeDrawingMaterialModel();
    internal string? Name40, Name50;
    internal int State48, State4C, State58, State5C, State78, State7C, State80, HasDspbr;
    internal NativeDrawingDspbrModel Dspbr = new NativeDrawingDspbrModel();
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy visual properties.");
        ar.U32(ref BackgroundColor); ar.U32(ref EdgeColor); ar.U32(ref FaceColor);
        Material.Serialize(ar);
        DrawingPreviewRecordModel.String(ar, ref Name40); ar.I32(ref State48); ar.I32(ref State4C);
        DrawingPreviewRecordModel.String(ar, ref Name50); ar.I32(ref State58); ar.I32(ref State5C);
        if (ar.DocumentVersion >= 0x144C) ar.I32(ref State78);
        if (ar.DocumentVersion >= 0x241A) ar.I32(ref State7C);
        if (ar.DocumentVersion >= 0x2795) ar.I32(ref State80);
        if (ar.DocumentVersion >= 0x3F09)
        {
            ar.I32(ref HasDspbr); if (HasDspbr != 0) Dspbr.Serialize(ar);
        }
    }
}

// uoMaterialProperties restore/save 0x3CAF90F0 / 0x3CAF99F0.
internal sealed class NativeDrawingMaterialModel : IMfcSerializable
{
    internal string? Name28, Name20, Name30, NameB8;
    internal double[] Scalars = new double[6];
    internal int HasTexture, HasAppearance, HasSecondaryTexture, State7C;
    internal int[] States = new int[5];
    internal NativeDrawingTextureModel Texture = new NativeDrawingTextureModel(), SecondaryTexture = new NativeDrawingTextureModel();
    internal NativeDrawingRvAppearanceModel Appearance = new NativeDrawingRvAppearanceModel();
    internal NativeDrawingUserProperties Properties = new NativeDrawingUserProperties();
    public void Serialize(MfcArchive ar)
    {
        DrawingPreviewRecordModel.String(ar, ref Name28); DrawingPreviewRecordModel.String(ar, ref Name20);
        for (int i = 0; i < Scalars.Length; i++) ar.F64(ref Scalars[i]);
        ar.I32(ref HasTexture);
        if (HasTexture != 0) Texture.Serialize(ar);
        for (int i = 0; i < States.Length; i++) ar.I32(ref States[i]);
        DrawingPreviewRecordModel.String(ar, ref Name30); ar.I32(ref State7C); ar.I32(ref HasAppearance);
        if (HasAppearance != 0) Appearance.Serialize(ar);
        if (ar.DocumentVersion >= 0x3369) DrawingPreviewRecordModel.String(ar, ref NameB8);
        if (ar.DocumentVersion >= 0x372B)
        {
            ar.I32(ref HasSecondaryTexture);
            if (HasSecondaryTexture != 0) SecondaryTexture.Serialize(ar);
        }
        Properties.Serialize(ar);
    }
}

// uoDSPBRProperties_c restore/save 0x3CA26CF0 / 0x3CA28570.
// Native conversion on load changes several in-memory offsets; the wire order
// below retains the original values instead of re-applying conversion defaults.
internal sealed class NativeDrawingDspbrModel : IMfcSerializable
{
    internal float[] Surface = new float[26], Coating = new float[18], End = new float[2];
    internal int StateCC, StateD0, StateC8;
    internal string?[] Textures = new string?[5], ExtraTextures = new string?[2], ExtendedTextures = new string?[2], FinalTextures = new string?[3];
    internal float Scalar128;
    internal float[] Extended = new float[6], Final = new float[2];

    public void Serialize(MfcArchive ar)
    {
        Floats(ar, Surface); ar.I32(ref StateCC); Floats(ar, Coating); ar.I32(ref StateD0); Floats(ar, End);
        Strings(ar, Textures); ar.F32(ref Scalar128);
        if (ar.DocumentVersion >= 0x46B5)
        {
            Strings(ar, ExtraTextures); ar.I32(ref StateC8); Floats(ar, Extended); Strings(ar, ExtendedTextures);
        }
        if (ar.DocumentVersion >= 0x4972) { Floats(ar, Final); Strings(ar, FinalTextures); }
    }
    private static void Floats(MfcArchive ar, float[] values)
    {
        for (int i = 0; i < values.Length; i++) ar.F32(ref values[i]);
    }
    private static void Strings(MfcArchive ar, string?[] values)
    {
        for (int i = 0; i < values.Length; i++) DrawingPreviewRecordModel.String(ar, ref values[i]);
    }
}
