using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moRefGeomFeatureDataCollector Serialize 0x4B6A8900: high DWORD first,
// low DWORD second, then an inline discriminator and its virtual serializer.
internal sealed class NativeDrawingReferenceGeometryModel : IMfcSerializable
{
    internal sealed class Entry : IMfcSerializable
    {
        internal int Type;
        internal string? Name, ExtraName, ExtraText1, ExtraText2;
        internal int[] Header = new int[3], States = new int[5], ExtraStates = new int[8];
        internal DrawingPreviewPoint[] Points = new DrawingPreviewPoint[4];
        internal double[] Scalars = new double[4], ExtraScalars = new double[4];
        internal DrawingPreviewPoint ExtraVector, ExtraPoint;
        internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
        public void Serialize(MfcArchive ar)
        {
            ar.I32(ref Type);
            if (Type < 0 || Type > 6) throw new InvalidDataException("Invalid native reference-geometry type.");
            DrawingPreviewRecordModel.String(ar, ref Name);
            for (int i = 0; i < Header.Length; i++) ar.I32(ref Header[i]);
            if (Type == 5) { DrawingPreviewRecordModel.String(ar, ref ExtraName); ar.I32(ref States[0]); }
            int points = Type == 0 ? 0 : Type == 1 ? 1 : Type == 2 || Type == 5 ? 4 : Type == 3 || Type == 6 ? 2 : 3;
            for (int i = 0; i < points; i++) DrawingPreviewRecordModel.Point(ar, ref Points[i]);
            switch (Type)
            {
                case 2: ar.F64(ref Scalars[0]); ar.F64(ref Scalars[1]); break;
                case 3:
                    Transform.SerializeOperator(ar);
                    for (int i = 0; i < 4; i++) ar.F64(ref Scalars[i]);
                    ar.I32(ref States[0]); ar.I32(ref States[1]); break;
                case 4:
                    for (int i = 0; i < 5; i++) ar.I32(ref States[i]);
                    DrawingPreviewRecordModel.Point(ar, ref Points[3]); break;
                case 5:
                    ar.F64(ref Scalars[0]); ar.F64(ref Scalars[1]);
                    ar.F64(ref ExtraScalars[0]); ar.F64(ref ExtraScalars[1]);
                    DrawingPreviewRecordModel.Point(ar, ref ExtraVector); DrawingPreviewRecordModel.Point(ar, ref ExtraPoint);
                    ar.I32(ref ExtraStates[0]); ar.I32(ref ExtraStates[1]); ar.F64(ref ExtraScalars[2]);
                    ar.I32(ref ExtraStates[2]); ar.I32(ref ExtraStates[3]); ar.I32(ref ExtraStates[4]);
                    ar.F64(ref ExtraScalars[3]); ar.I32(ref ExtraStates[5]); ar.I32(ref ExtraStates[6]);
                    DrawingPreviewRecordModel.String(ar, ref ExtraText1); DrawingPreviewRecordModel.String(ar, ref ExtraText2);
                    ar.I32(ref ExtraStates[7]); break;
            }
        }
    }
    internal Entry[] Entries = Array.Empty<Entry>();
    public void Serialize(MfcArchive ar)
    {
        uint high = 0, low = (uint)Entries.Length; ar.U32(ref high); ar.U32(ref low);
        if (high != 0 || low > int.MaxValue || ar.IsLoading && low > ar.Remaining / 20)
            throw new InvalidDataException("Invalid native reference-geometry count.");
        if (ar.IsLoading) Entries = new Entry[low];
        for (int i = 0; i < Entries.Length; i++)
        {
            if (ar.IsLoading) Entries[i] = new Entry();
            Entries[i].Serialize(ar);
        }
    }
}
