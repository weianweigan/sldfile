using System;
using System.IO;
using System.Linq;
using SldFile.Drawing;

namespace SldFile.Mfc;

/// <summary>Observed six-channel uoTempFaceTessData_c table; all channels participate in both directions.</summary>
internal sealed class DrawingTessellationModel : IMfcSerializable
{
    internal uint[] Strips = Array.Empty<uint>(), EndpointCounts = Array.Empty<uint>();
    internal float[] Positions = Array.Empty<float>(), Normals = Array.Empty<float>(), Endpoints = Array.Empty<float>();
    internal byte[] EndpointFlags = Array.Empty<byte>();
    public void Serialize(MfcArchive ar)
    {
        for (int channel = 0; channel < 6; channel++)
        {
            uint expectedSize = channel == 1 || channel == 2 ? 12u : channel == 5 ? 1u : 4u;
            uint expectedKind = channel == 1 || channel == 2 ? 100u : 8u;
            uint size = expectedSize, kind = expectedKind, flags = 2;
            uint count = channel == 0 ? (uint)Strips.Length : channel == 1 ? (uint)Positions.Length / 3 :
                channel == 2 ? (uint)Normals.Length / 3 : channel == 3 ? (uint)Endpoints.Length :
                channel == 4 ? (uint)EndpointCounts.Length : (uint)EndpointFlags.Length;
            ar.U32(ref size); ar.U32(ref kind); ar.U32(ref flags); ar.U32(ref count);
            if (size != expectedSize || kind != expectedKind || flags != 2 || count > 2000000 || ar.IsLoading && (long)count * size > ar.Remaining)
                throw new InvalidDataException("Invalid native face-tessellation channel.");
            if (channel == 0 || channel == 4)
            {
                uint[] values = channel == 0 ? Strips : EndpointCounts;
                if (ar.IsLoading) values = new uint[count];
                for (int i = 0; i < values.Length; i++) ar.U32(ref values[i]);
                if (channel == 0) Strips = values; else EndpointCounts = values;
            }
            else if (channel == 5)
            {
                if (ar.IsLoading) EndpointFlags = new byte[count];
                for (int i = 0; i < EndpointFlags.Length; i++) ar.Byte(ref EndpointFlags[i]);
            }
            else
            {
                float[] values = channel == 1 ? Positions : channel == 2 ? Normals : Endpoints;
                if (ar.IsLoading) values = new float[count * (size / 4)];
                for (int i = 0; i < values.Length; i++)
                {
                    ar.F32(ref values[i]);
                    if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) throw new InvalidDataException("Nonfinite face data.");
                }
                if (channel == 1) Positions = values; else if (channel == 2) Normals = values; else Endpoints = values;
            }
        }
        if (Strips.Length == 0 || Strips.Any(s => s < 3) || Strips.Sum(s => (long)s) != Positions.Length / 3 ||
            Normals.Length != 0 && Normals.Length != Positions.Length || EndpointCounts.Length != Strips.Length)
            throw new InvalidDataException("Inconsistent native tessellation strips.");
        for (int i = 0; i < Strips.Length; i++)
            if (EndpointCounts[i] != (long)Strips[i] * 2 - 2) throw new InvalidDataException("Inconsistent strip endpoints.");
        long endpointCount = EndpointCounts.Sum(n => (long)n);
        if (!((Endpoints.Length == 0 && EndpointFlags.Length == 0) || (Endpoints.Length == endpointCount && EndpointFlags.Length == endpointCount)))
            throw new InvalidDataException("Incomplete endpoint channels.");
    }
    internal DrawingPreviewPoint Point(int index) => new DrawingPreviewPoint(Positions[index * 3], Positions[index * 3 + 1], Positions[index * 3 + 2]);
}
