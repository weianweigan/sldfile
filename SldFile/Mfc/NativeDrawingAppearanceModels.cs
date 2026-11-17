using System;

namespace SldFile.Mfc;

// uoMaterialTextureProperties restore/save 0x3CB7DB90 / 0x3CB7DD90.
internal sealed class NativeDrawingTextureModel : IMfcSerializable
{
    internal double[] Coordinates = new double[13];
    internal string? Path;
    internal short Type;
    internal int State7C, State9C;
    internal double Scalar80, Scalar88;
    public void Serialize(MfcArchive ar)
    {
        for (int i = 0; i < Coordinates.Length; i++) ar.F64(ref Coordinates[i]);
        DrawingPreviewRecordModel.String(ar, ref Path); ar.I16(ref Type); ar.I32(ref State7C);
        if (ar.DocumentVersion >= 0xF1E) { ar.F64(ref Scalar80); ar.F64(ref Scalar88); }
        if (ar.DocumentVersion >= 0x372B) ar.I32(ref State9C);
    }
}

// uoRVAppearanceProperties restore/save 0x3CB3E900 / 0x3CB3EFA0.
internal sealed class NativeDrawingRvAppearanceModel : IMfcSerializable
{
    internal uint Type, Version, Color, TextureColor;
    internal string? Name, TexturePath, BumpPath, NormalPath;
    internal byte State10;
    internal float[] Surface = new float[10], Lobe = new float[5], Reflection = new float[2], Bump = new float[3], Coating = new float[8], SurfaceD0 = new float[2], SurfaceDC = new float[2];
    internal int State40, State58, State9C, StateA0;
    internal float Scalar5C, Scalar60, Scalar74, Scalar78, Scalar94, ScalarD8, Scalar98;
    internal string?[] LegacyNames = new string?[3];
    internal float[] LegacyScalars = new float[17];
    internal int[] LegacyStates = new int[6];
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy RV appearance.");
        ar.U32(ref Type); ar.U32(ref Version); DrawingPreviewRecordModel.String(ar, ref Name);
        ar.Byte(ref State10); ar.U32(ref Color); Floats(ar, Surface);
        ar.I32(ref State40); Floats(ar, Lobe); ar.I32(ref State58);
        ar.F32(ref Scalar5C); ar.F32(ref Scalar60); DrawingPreviewRecordModel.String(ar, ref TexturePath);
        ar.U32(ref TextureColor); ar.F32(ref Scalar74); ar.F32(ref Scalar78);
        DrawingPreviewRecordModel.String(ar, ref BumpPath); Floats(ar, Bump); ar.I32(ref State9C);
        ar.F32(ref Scalar94); ar.I32(ref StateA0); Floats(ar, Coating); Floats(ar, SurfaceD0); ar.F32(ref ScalarD8);
        if (ar.DocumentVersion >= 0x1416) Floats(ar, SurfaceDC);
        if (ar.DocumentVersion >= 0x1422) DrawingPreviewRecordModel.String(ar, ref NormalPath);
        if (ar.DocumentVersion >= 0x1463) ar.F32(ref Scalar98);
        if (ar.DocumentVersion >= 0x3348 && ar.DocumentVersion < 0x46BA)
        {
            for (int i = 0; i < LegacyNames.Length; i++) DrawingPreviewRecordModel.String(ar, ref LegacyNames[i]);
            Floats(ar, LegacyScalars);
            for (int i = 0; i < LegacyStates.Length; i++) ar.I32(ref LegacyStates[i]);
        }
    }
    private static void Floats(MfcArchive ar, float[] values)
    {
        for (int i = 0; i < values.Length; i++) ar.F32(ref values[i]);
    }
}
