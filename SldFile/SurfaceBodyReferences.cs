namespace SldFile;

/// <summary>
/// Presence-level reference to the design-tree <c>moSurfaceBodyFolder_c</c> node.
/// The controlled 2026 samples do not serialize individual surface-body ids in the
/// ResolvedFeatures stream, so this model deliberately exposes only the validated
/// folder header (presence and opaque id fields), never a guessed body id.
/// </summary>
public sealed class SurfaceBodyFolderReference
{
    /// <summary>Serialized MFC class name; observed value is <c>moSurfaceBodyFolder_c</c>.</summary>
    public string ClassName => "moSurfaceBodyFolder_c";

    /// <summary>FolderHeader TypeId; observed value 0x0A in the controlled samples.</summary>
    public uint SerializedTypeId { get; internal set; }

    /// <summary>FolderHeader MarkerE value (sample-dependent).</summary>
    public uint MarkerE { get; internal set; }

    /// <summary>FolderHeader ChildFlag; observed value 4.</summary>
    public uint ChildFlag { get; internal set; }

    /// <summary>FolderHeader SubType; observed value 2.</summary>
    public uint SubType { get; internal set; }

    /// <summary>FolderHeader VersionFlag; observed value 0x00010000.</summary>
    public uint VersionFlag { get; internal set; }

    /// <summary>FolderHeader HasData; observed value 0x00010000.</summary>
    public uint HasData { get; internal set; }

    /// <summary>True because a validated surface-body folder node was found.</summary>
    public bool IsPresent => true;
}
