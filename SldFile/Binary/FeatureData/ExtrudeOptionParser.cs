using System;

namespace SldFile.Binary;

/// <summary>Fields established by the native extrusion option experiment dated 2026-10-30.</summary>
internal static class ExtrudeOptionParser
{
    // This layout was established for moExtrusion_c, stream 0x70. Do not apply it
    // to Cut's class references or older streams without independent evidence.
    internal static bool? ReadReverseDirection(ushort streamVersion, string className, byte[]? endSpec)
    {
        if (streamVersion != 0x70 || className != "moExtrusion_c" || endSpec == null || endSpec.Length < 24)
            return null;
        if (BitConverter.ToUInt16(endSpec, 0) != 0 ||
            BitConverter.ToUInt32(endSpec, 2) > 1 ||
            BitConverter.ToUInt32(endSpec, 6) != 0 ||
            BitConverter.ToUInt16(endSpec, 14) != 0)
            return null;
        uint reverse = BitConverter.ToUInt32(endSpec, 10);
        return reverse <= 1 ? reverse == 1 : (bool?)null;
    }
}
