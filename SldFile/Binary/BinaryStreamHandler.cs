using System;
using System.IO;
using OpenMcdf;

namespace SldFile.Binary;

/// <summary>
/// Interface for reading and parsing a specific binary stream into a strongly-typed model.
/// </summary>
internal interface IBinaryStreamReader<in T> where T : class
{
    string StreamName { get; }
    void Parse(byte[] data, T model);
}

/// <summary>
/// Base class for reading raw binary streams from storage or byte buffers.
/// </summary>
internal abstract class BinaryStreamReader<T> : IBinaryStreamReader<T> where T : class
{
    public abstract string StreamName { get; }

    public void Read(Storage root, T model)
    {
        if (!root.TryOpenStream(StreamName, out var stream))
        {
            return;
        }

        int length = checked((int)stream.Length);
        var bytes = new byte[length];
        int totalRead = 0;

        while (totalRead < length)
        {
            int read = stream.Read(bytes, totalRead, length - totalRead);
            if (read <= 0)
            {
                throw new EndOfStreamException($"Unexpected EOF while reading stream '{StreamName}': expected {length} bytes, got {totalRead}.");
            }

            totalRead += read;
        }

        Parse(bytes, model);
    }

    public abstract void Parse(byte[] data, T model);
}
