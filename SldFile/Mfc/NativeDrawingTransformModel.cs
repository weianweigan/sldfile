using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// sldmgu.dll: mgXform_c::save 0x3216F510, restore 0x3216EA30,
// iSavePtr 0x3216CAD0; mgMatrix5_c::save 0x3216F160.
// Native object payload only: the containing serializer owns the boundary.
internal sealed class NativeDrawingTransformModel : IMfcSerializable
{
    internal byte HasRotation;
    internal double[] Matrix = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
    internal DrawingPreviewPoint Translation;
    internal double Scale = 1;
    internal byte Flags;
    internal int Matrix5Version;
    internal double[] Extension = Array.Empty<double>();
    internal int MatrixState;
    internal uint MatrixPadding, OperatorFlags, OperatorReserved;

    public void Serialize(MfcArchive ar)
    {
        ar.Byte(ref HasRotation);
        if (HasRotation != 0)
            for (int i = 0; i < 9; i++) ar.F64(ref Matrix[i]);
        else if (ar.IsLoading) Matrix = new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        DrawingPreviewRecordModel.Point(ar, ref Translation);
        ar.F64(ref Scale);
        ar.Byte(ref Flags);
        SerializeExtension(ar);
    }

    // operator >> 0x322803B0 / operator << 0x3227CDD0 serialize 30 DWORDs,
    // unlike save/restore. Keep the matrix's state and padding as wire fields.
    internal void SerializeOperator(MfcArchive ar)
    {
        for (int i = 0; i < 9; i++) ar.F64(ref Matrix[i]);
        ar.I32(ref MatrixState); ar.U32(ref MatrixPadding);
        DrawingPreviewRecordModel.Point(ar, ref Translation);
        ar.F64(ref Scale); ar.U32(ref OperatorFlags); ar.U32(ref OperatorReserved);
        if (ar.IsLoading) Flags = (byte)OperatorFlags;
        SerializeExtension(ar);
    }

    private void SerializeExtension(MfcArchive ar)
    {
        int extensionCount;
        switch (Flags & 0xF0)
        {
            case 0: extensionCount = 0; break;
            case 0x10: extensionCount = 4; break; // mgPerspInfoBase_c
            case 0x20: extensionCount = 6; break; // mgPerspInfo_c
            case 0x40:
                ar.I32(ref Matrix5Version);
                extensionCount = 25;
                break;
            default: throw new InvalidDataException("Unsupported native transform extension flags.");
        }
        if (ar.IsLoading)
        {
            if (extensionCount > ar.Remaining / 8) throw new EndOfStreamException();
            Extension = new double[extensionCount];
        }
        if (Extension.Length != extensionCount)
            throw new InvalidDataException("Native transform extension does not match its flags.");
        for (int i = 0; i < Extension.Length; i++) ar.F64(ref Extension[i]);
    }

    internal DrawingPreviewPoint TransformPoint(DrawingPreviewPoint p)
    {
        double s = Math.Abs(Scale) > 1e-12 && !double.IsNaN(Scale) && !double.IsInfinity(Scale) ? Scale : 1.0;
        if (HasRotation != 0 && Matrix != null && Matrix.Length >= 9)
        {
            double rx = Matrix[0] * p.X + Matrix[1] * p.Y + Matrix[2] * p.Z;
            double ry = Matrix[3] * p.X + Matrix[4] * p.Y + Matrix[5] * p.Z;
            double rz = Matrix[6] * p.X + Matrix[7] * p.Y + Matrix[8] * p.Z;
            return new DrawingPreviewPoint(rx * s + Translation.X, ry * s + Translation.Y, rz * s + Translation.Z);
        }
        return new DrawingPreviewPoint(p.X * s + Translation.X, p.Y * s + Translation.Y, p.Z * s + Translation.Z);
    }

    internal double GetRotationAngle()
    {
        if (HasRotation == 0 || Matrix == null || Matrix.Length < 4) return 0.0;
        return Math.Atan2(Matrix[3], Matrix[0]);
    }
}

// sldmfcu.dll utLineWidth_c::Serialize 0x3CAF29A0. The legacy
// RestoreType selects a signed byte, signed short or signed int; 0 has no data.
internal sealed class NativeDrawingLineWidthModel : IMfcSerializable
{
    internal int RestoreType;
    internal bool ForceModernLayout;
    internal short Type;
    internal float Width;
    internal int LegacyValue;

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion <= 0)
            throw new InvalidDataException("A native document version is required for line width.");
        if (ForceModernLayout || ar.DocumentVersion >= 0xF41)
        {
            ar.I16(ref Type);
            ar.F32(ref Width);
        }
        else
        {
            switch (RestoreType)
            {
                case 0: break;
                case 1:
                    byte value = unchecked((byte)LegacyValue); ar.Byte(ref value);
                    if (ar.IsLoading) { LegacyValue = (sbyte)value; Type = (short)LegacyValue; }
                    break;
                case 2:
                    short narrow = unchecked((short)LegacyValue); ar.I16(ref narrow);
                    if (ar.IsLoading) { LegacyValue = narrow; Type = narrow; }
                    break;
                case 3:
                    ar.I32(ref LegacyValue);
                    if (ar.IsLoading) Type = unchecked((short)LegacyValue);
                    break;
                default: throw new InvalidDataException("Unknown native line-width restore type.");
            }
        }
    }
}
