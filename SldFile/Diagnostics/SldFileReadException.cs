using System;

namespace SldFile.Diagnostics;

/// <summary>
/// Base exception for all SldFile library errors.
/// </summary>
public class SldFileException : Exception
{
    public SldFileException()
    {
    }

    public SldFileException(string message) : base(message)
    {
    }

    public SldFileException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Thrown when an OLE stream cannot be found in the compound file.
/// This is a recoverable error — the caller can decide to use default values.
/// </summary>
internal class StreamNotFoundException : SldFileException
{
    public string StreamName { get; }

    public StreamNotFoundException(string streamName)
        : base($"OLE stream '{streamName}' was not found in the compound file.")
    {
        StreamName = streamName;
    }

    public StreamNotFoundException(string streamName, Exception inner)
        : base($"OLE stream '{streamName}' was not found in the compound file.", inner)
    {
        StreamName = streamName;
    }
}

/// <summary>
/// Thrown when the binary layout of an OLE stream is corrupted or does not match
/// the expected format (invalid offsets, truncated data, etc.).
/// </summary>
internal class StreamCorruptedException : SldFileException
{
    public string StreamName { get; }

    public StreamCorruptedException(string streamName, string detail)
        : base($"OLE stream '{streamName}' is corrupted: {detail}")
    {
        StreamName = streamName;
    }

    public StreamCorruptedException(string streamName, string detail, Exception inner)
        : base($"OLE stream '{streamName}' is corrupted: {detail}", inner)
    {
        StreamName = streamName;
    }
}

/// <summary>
/// Thrown when an individual OLE property within a stream cannot be parsed
/// (e.g. unsupported VT type, out-of-range offset within the property set).
/// </summary>
internal class PropertyReadException : SldFileException
{
    public string StreamName { get; }
    public uint PropertyId { get; }

    public PropertyReadException(string streamName, uint propertyId, string detail)
        : base($"Failed to read property {propertyId} from stream '{streamName}': {detail}")
    {
        StreamName = streamName;
        PropertyId = propertyId;
    }

    public PropertyReadException(string streamName, uint propertyId, string detail, Exception inner)
        : base($"Failed to read property {propertyId} from stream '{streamName}': {detail}", inner)
    {
        StreamName = streamName;
        PropertyId = propertyId;
    }
}
