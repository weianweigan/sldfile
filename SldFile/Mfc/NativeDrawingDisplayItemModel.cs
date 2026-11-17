using System;
using System.Collections.Generic;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// sldmfcu.dll uoDisplayItem_c restore/save dispatch: 0x3CBA1350 /
// 0x3CBA3380. Fields below are typed wire values, not an opaque replay buffer.
// Their order is defined by each native payload serializer and document version.
internal sealed class NativeDrawingDisplayItemModel : IMfcSerializable
{
    internal uint MemoryFootprint;
    internal int Type;
    internal int Offset;
    internal readonly List<int> Integers = new List<int>();
    internal readonly List<double> Scalars = new List<double>();
    internal readonly List<float> Floats = new List<float>();
    internal readonly List<ushort> Shorts = new List<ushort>();
    internal readonly List<byte> Bytes = new List<byte>();
    internal readonly List<string?> Strings = new List<string?>();
    internal readonly List<DrawingPreviewPoint> Points = new List<DrawingPreviewPoint>();
    internal readonly List<NativeDrawingDisplayItemModel> Children = new List<NativeDrawingDisplayItemModel>();
    internal NativeDrawingTransformModel? Transform;
    internal NativeDrawingLineWidthModel LineWidth = new NativeDrawingLineWidthModel { RestoreType = 3 };

    public void Serialize(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        if (version < 5000) throw new NotSupportedException("Native display item versions before 5000 are not verified.");
        if (ar.IsLoading)
        {
            Offset = ar.Position;
            Integers.Clear(); Scalars.Clear(); Floats.Clear(); Shorts.Clear(); Bytes.Clear(); Strings.Clear(); Points.Clear(); Children.Clear();
            Transform = null;
        }
        ar.U32(ref MemoryFootprint); ar.I32(ref Type);
        var fields = new Fields(this, ar);
        switch (Type)
        {
            case 0: break;
            case 1: fields.Points(1); fields.Ints(2); break;
            case 2: fields.Points(4); fields.Ints(1); break;
            case 3: fields.Points(5); fields.Doubles(2); break;
            case 4: fields.Points(2); fields.Byte(); break;
            case 5: fields.Points(fields.Count(24)); fields.Ints(1); break;
            case 6: fields.Ints(1); fields.Points(fields.Count(24)); break;
            case 7:
            case 15:
                fields.Points(1); fields.Doubles(2); fields.Ints(2);
                string? text = fields.String();
                if (version >= 0x72B) { fields.Ints(1); fields.Doubles(2); }
                if (version >= 0xA04 && fields.Short() != 0) fields.Reals(text?.Length ?? 0);
                break;
            case 8:
                fields.Points(1); Xform(ar); fields.Doubles(1); fields.String();
                break;
            case 9:
                fields.Ints(4); LineWidth.Serialize(ar); fields.Ints(2);
                if (version >= 0x185A) fields.Ints(1);
                break;
            case 10:
            case 14:
                fields.Doubles(1); fields.Ints(1); fields.Doubles(2); fields.Ints(1); fields.String();
                fields.Doubles(2);
                break;
            case 11: fields.String(); break;
            case 12:
                fields.Ints(1); LineWidth.Serialize(ar); fields.Ints(2); fields.String();
                break;
            case 13:
                fields.Points(5);
                if (version >= 0x13ED) fields.Ints(1);
                fields.Doubles(2);
                break;
            case 18:
                fields.Points(1);
                if (fields.Int() != 0) Xform(ar);
                fields.Doubles(1); fields.Ints(1);
                fields.String();
                if (fields.Short() != 0) fields.Reals(fields.Count(4));
                if (version >= 0x1448 && version < 0x144A) fields.Ints(1);
                if (version >= 0x1C05 && version < 0x1FB5) fields.Reals(1);
                if (version >= 0x2001) fields.Ints(1);
                if (version >= 0x239B) fields.Doubles(1);
                break;
            case 19:
                fields.Points(1);
                if (fields.Int() != 0) Xform(ar);
                fields.Doubles(2); fields.String(); fields.String();
                if (version >= 0x3720) Nested(ar, fields.Count(8));
                break;
            case 23:
                fields.Ints(2);
                if (fields.Int() == 0) fields.Ints(fields.Count(4));
                fields.String();
                if (version >= 0x1C57) fields.Ints(1);
                if (version >= 0x238D) fields.Ints(1);
                if (version >= 0x3333) fields.String();
                break;
            case 24:
            case 32:
            case 116:
                fields.Ints(1); fields.Points(1); fields.Ints(2);
                break;
            case 25:
            case 26: fields.Ints(1); break;
            case 28: fields.Points(3); fields.Ints(1); break;
            case 29:
                fields.Ints(2);
                if (version >= 0x1C16 && version < 0x1C4A) fields.Ints(fields.Count(4));
                if (version >= 0x1C4A)
                {
                    int groups = fields.Count(4);
                    for (int i = 0; i < groups; i++)
                    {
                        fields.Ints(1); fields.Ints(fields.Count(4)); fields.Points(fields.Count(24));
                        if (version >= 0x1C4C) fields.Strings(fields.Count(4));
                        if (version >= 0x1FA9) fields.Ints(1);
                        if (version >= 0x1FD5) fields.Strings(fields.Count(4));
                        if (version >= 0x23FF) fields.String();
                        if (version >= 0x332C) fields.Strings(2);
                    }
                }
                if (version >= 0x35E9) fields.String();
                break;
            case 30: fields.Points(1); fields.Ints(1); break;
            case 31:
                int vertices = fields.Count(48);
                fields.Doubles(1);
                if (version >= 0x2016) fields.Ints(1);
                fields.Points(vertices); fields.Points(vertices);
                if (version >= 0x1FF8) fields.String();
                break;
            case 33:
                fields.Points(1); fields.Ints(1);
                if (version >= 0x146F) { fields.Doubles(1); fields.Ints(1); }
                if (version >= 0x1473) fields.Doubles(1);
                break;
            case 100: fields.Points(2); fields.Ints(1); break;
            case 101: fields.Points(4); fields.Ints(1); break;
            case 108: fields.Reals(1); break;
            case 113: fields.Ints(2); fields.Reals(1); break;
            default: throw new NotSupportedException($"Unsupported native display item type {Type} at {Offset}.");
        }
        fields.Complete();
    }

    private void Xform(MfcArchive ar)
    {
        if (ar.IsLoading) Transform = new NativeDrawingTransformModel();
        (Transform ?? throw new InvalidDataException("Missing native display transform.")).Serialize(ar);
    }

    private void Nested(MfcArchive ar, int count)
    {
        if (!ar.IsLoading && Children.Count != count) throw new InvalidDataException("Native symbol child count changed.");
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) Children.Add(new NativeDrawingDisplayItemModel());
            Children[i].Serialize(ar);
        }
    }

    internal static void List(MfcArchive ar, ref NativeDrawingDisplayItemModel[] items)
    {
        uint count = (uint)items.Length; ar.Count(ref count);
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 8) throw new InvalidDataException("Invalid native display-item count.");
            items = new NativeDrawingDisplayItemModel[count];
        }
        for (int i = 0; i < items.Length; i++)
        {
            if (ar.IsLoading) items[i] = new NativeDrawingDisplayItemModel();
            items[i].Serialize(ar);
        }
    }

    // Typed field cursors keep one load/store layout. No binary payload is replayed.
    private sealed class Fields
    {
        private readonly NativeDrawingDisplayItemModel _model;
        private readonly MfcArchive _ar;
        private int _ints, _doubles, _floats, _shorts, _bytes, _strings, _points;
        internal Fields(NativeDrawingDisplayItemModel model, MfcArchive ar) { _model = model; _ar = ar; }
        internal int Int()
        {
            int value = _ar.IsLoading ? 0 : _model.Integers[_ints]; _ar.I32(ref value);
            if (_ar.IsLoading) _model.Integers.Add(value); _ints++; return value;
        }
        internal int Count(int minimumSize)
        {
            int count = Int();
            if (count < 0 || _ar.IsLoading && count > _ar.Remaining / minimumSize)
                throw new InvalidDataException("Invalid native display payload count.");
            return count;
        }
        internal void Ints(int count) { for (int i = 0; i < count; i++) Int(); }
        internal void Doubles(int count)
        {
            for (int i = 0; i < count; i++)
            {
                double value = _ar.IsLoading ? 0 : _model.Scalars[_doubles]; _ar.F64(ref value);
                if (_ar.IsLoading) _model.Scalars.Add(value); _doubles++;
            }
        }
        internal void Reals(int count)
        {
            if (_ar.IsLoading && count > _ar.Remaining / 4) throw new EndOfStreamException();
            for (int i = 0; i < count; i++)
            {
                float value = _ar.IsLoading ? 0 : _model.Floats[_floats]; _ar.F32(ref value);
                if (_ar.IsLoading) _model.Floats.Add(value); _floats++;
            }
        }
        internal ushort Short()
        {
            ushort value = _ar.IsLoading ? (ushort)0 : _model.Shorts[_shorts]; _ar.U16(ref value);
            if (_ar.IsLoading) _model.Shorts.Add(value); _shorts++; return value;
        }
        internal void Byte()
        {
            byte value = _ar.IsLoading ? (byte)0 : _model.Bytes[_bytes]; _ar.Byte(ref value);
            if (_ar.IsLoading) _model.Bytes.Add(value); _bytes++;
        }
        internal string? String()
        {
            string? value = _ar.IsLoading ? null : _model.Strings[_strings];
            DrawingPreviewRecordModel.String(_ar, ref value);
            if (_ar.IsLoading) _model.Strings.Add(value); _strings++; return value;
        }
        internal void Strings(int count) { for (int i = 0; i < count; i++) String(); }
        internal void Points(int count)
        {
            if (_ar.IsLoading && count > _ar.Remaining / 24) throw new EndOfStreamException();
            for (int i = 0; i < count; i++)
            {
                var point = _ar.IsLoading ? default : _model.Points[_points];
                DrawingPreviewRecordModel.Point(_ar, ref point);
                if (_ar.IsLoading) _model.Points.Add(point); _points++;
            }
        }
        internal void Complete()
        {
            if (_ints != _model.Integers.Count || _doubles != _model.Scalars.Count || _floats != _model.Floats.Count ||
                _shorts != _model.Shorts.Count || _bytes != _model.Bytes.Count || _strings != _model.Strings.Count || _points != _model.Points.Count)
                throw new InvalidDataException("Native display fields do not match the selected payload layout.");
        }
    }
}
