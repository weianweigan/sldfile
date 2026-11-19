namespace SldFile.Geometry;

/// <summary>
/// Options for exporting a 3D mesh to a standalone, offline single-file HTML/WebGL viewer.
/// </summary>
public sealed class Html3DExportOptions
{
    /// <summary>
    /// Title of the model shown in the header bar of the HTML viewer.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether to export outline CAD feature edges.
    /// </summary>
    public bool IncludeEdges { get; set; } = true;

    /// <summary>
    /// Whether to enable the assembly exploded view slider.
    /// </summary>
    public bool EnableExplode { get; set; } = true;

    /// <summary>
    /// Whether to enable interactive 3-axis section cutting tool.
    /// </summary>
    public bool EnableSectionCut { get; set; } = true;

    /// <summary>
    /// Whether to enable the component explorer / assembly parts list panel.
    /// </summary>
    public bool EnableComponentTree { get; set; } = true;

    /// <summary>
    /// Visual theme of the viewer: "dark" (default CAD slate) or "light".
    /// </summary>
    public string Theme { get; set; } = "dark";

    /// <summary>
    /// Geometric scale factor (default is 1.0f for mm).
    /// </summary>
    public float Scale { get; set; } = 1.0f;
}
