using System;
using System.IO;
using OpenMcdf;
using SldFile.Diagnostics;

namespace SldFile.Ole;

/// <summary>
/// Base class for all OLE stream readers.
/// Provides the common open-read-parse pipeline and error handling for both
/// <see cref="OlePropertySetSerializer"/>-based streams and
/// custom binary-parsed streams.
/// </summary>
/// <typeparam name="TModel">The strongly-typed model populated by this reader.</typeparam>
internal abstract class OleStreamReader<TModel> where TModel : class
{
    /// <summary>The OLE stream name this reader targets (e.g. "\u0005SummaryInformation").</summary>
    protected abstract string StreamName { get; }

    /// <summary>
    /// Reads the target stream from <paramref name="root"/> and populates <paramref name="model"/>.
    /// <para>
    /// If the stream does not exist, this method returns silently — the model retains its defaults.
    /// If the stream is corrupted, a <see cref="StreamCorruptedException"/> propagates.
    /// </para>
    /// </summary>
    /// <exception cref="StreamCorruptedException">The stream exists but cannot be parsed.</exception>
    public void Read(RootStorage root, TModel model)
    {
        CfbStream? stream;
        try
        {
            stream = OpenStream(root);
        }
        catch (StreamNotFoundException)
        {
            // Stream missing → silently keep model defaults.
            return;
        }

        if (stream == null)
            return;

        using (stream)
        {
            Parse(stream, model);
        }
    }

    /// <summary>
    /// Opens the OLE stream. Returns null if the stream does not exist.
    /// </summary>
    /// <exception cref="StreamNotFoundException">
    /// Thrown when the stream is not found (callers should treat this as recoverable).
    /// </exception>
    protected virtual CfbStream? OpenStream(RootStorage root)
    {
        try
        {
            return root.OpenStream(StreamName);
        }
        catch (FileNotFoundException ex)
        {
            throw new StreamNotFoundException(StreamName, ex);
        }
        catch (AggregateException ex) when (ex.InnerException is FileNotFoundException)
        {
            throw new StreamNotFoundException(StreamName, ex);
        }
    }

    /// <summary>
    /// Stream-specific parsing logic. Implemented by each concrete reader.
    /// </summary>
    protected abstract void Parse(CfbStream stream, TModel model);

    protected static byte[] ReadAllBytes(CfbStream stream)
    {
        int len = checked((int)stream.Length);
        var bytes = new byte[len];
        int totalRead = 0;
        while (totalRead < len)
        {
            int read = stream.Read(bytes, totalRead, len - totalRead);
            if (read <= 0) break;
            totalRead += read;
        }
        return bytes;
    }
}
