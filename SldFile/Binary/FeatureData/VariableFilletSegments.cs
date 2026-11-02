namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured variable-radius fillet layout from the SW2026
/// <c>FilletOptions/fillet_variable_2_4mm</c> control and its resave:
/// <c>VarFillet_c</c> / FolderHeader TypeId 0x52, one edge and two control-point radii
/// (D0=0.002 m, D01=0.004 m). The radii live in the named-parameter blocks embedded in
/// <c>ThreeDRadiusDim_c</c> (first control point) and <c>moVertexRef_c</c> (second
/// control point plus the negative copy of the first radius).
/// </summary>
internal static class VariableFilletSegments
{
    internal const string ClassName = "VarFillet_c";
    internal const uint TypeId = 0x52u;

    /// <summary>Measured VarFillet_c payload length (main and resave).</summary>
    internal const int HeaderExpectedLength = 311;

    // First control point: ThreeDRadiusDim_c.
    internal const int ThreeDRadiusExpectedLength = 1100;
    internal const int ThreeDRadiusParameterOffset = 6;
    internal const string FirstParameterName = "D0";
    internal static readonly int[] FirstRadiusPositiveSlots =
        { 38, 70, 94, 219, 318, 552, 576, 1024, 1048 };

    // Second control point: the radius parameter is embedded in moVertexRef_c at +830
    // (schema 0x8004) / D01 name at +832 / value at +864.
    internal const int VertexRefExpectedLength = 2228;
    internal const int VertexRefParameterOffset = 830;
    internal const string SecondParameterName = "D01";
    internal static readonly int[] SecondRadiusPositiveSlots =
        { 864, 904, 928, 1045, 1144, 1378, 1402, 1850, 1874 };
    /// <summary>Measured negative copy of half the second radius inside moVertexRef_c (-D01/2).</summary>
    internal const int VertexRefSecondRadiusHalfNegativeSlot = 1136;

    // Surrounding measured objects (presence/length gates for the strict layout).
    internal const int SurfaceIdRepLength = 14;
    internal const int FilletSurfIdRepLength = 20;
    internal const int EdgeRefLength = 146;
    internal const int CompEdgeLength = 162;
    internal const int DisplayRadialDimLength = 665;
    internal const int CompVertexLength = 86;
}
