using System;
using System.Collections.Generic;

namespace SldFile.Binary.FeatureData;

internal sealed class LegacyHoleWizardField
{
    internal LegacyHoleWizardField(string property, string className, int classTagOffset, int offset, bool isString, string? expectedString, double expectedDouble)
    {
        Property = property; ClassName = className; ClassTagOffset = classTagOffset; Offset = offset; IsString = isString;
        ExpectedString = expectedString; ExpectedDouble = expectedDouble;
    }
    internal string Property { get; }
    internal string ClassName { get; }
    internal int ClassTagOffset { get; }
    internal int Offset { get; }
    internal bool IsString { get; }
    internal string? ExpectedString { get; }
    internal double ExpectedDouble { get; }
}

internal sealed class LegacyHoleWizardFamily
{
    internal LegacyHoleWizardFamily(ushort version, ushort folderCount, uint headerTypeId, int restLength, int type, int endCondition, LegacyHoleWizardField[] fields)
    {
        Version = version; FolderCount = folderCount; HeaderTypeId = headerTypeId; RestLength = restLength;
        Type = type; EndCondition = endCondition; Fields = fields;
    }
    internal ushort Version { get; }
    internal ushort FolderCount { get; }
    internal uint HeaderTypeId { get; }
    internal int RestLength { get; }
    internal int Type { get; }
    internal int EndCondition { get; }
    internal LegacyHoleWizardField[] Fields { get; }
}

internal static class LegacyHoleWizardTutorialTemplates
{
    internal static readonly LegacyHoleWizardFamily[] Families =
    {
        new LegacyHoleWizardFamily((ushort)0x3CE, (ushort)52, 0xE2u, 4248, 0, 0, new LegacyHoleWizardField[]
        {
        }),
        new LegacyHoleWizardFamily((ushort)0x136, (ushort)61, 0x17u, 882, 0, 1, new LegacyHoleWizardField[]
        {
        }),
        new LegacyHoleWizardFamily((ushort)0x1CC, (ushort)55, 0x112u, 7476, 0, 0, new LegacyHoleWizardField[]
        {
        }),
        new LegacyHoleWizardFamily((ushort)0xBD, (ushort)40, 0x4Du, 1398, 46, 0, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moCylinderRef_w", 33951, 2525, true, "ANSI Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moCylinderRef_w", 33951, 2547, true, "Straight Pipe Tapped hole", 0),
            new LegacyHoleWizardField("FastenerSize", "moCylinderRef_w", 33951, 2601, true, "3/4-14 NPSM", 0),
            new LegacyHoleWizardField("ThreadDiameter", "ThreeDdiameterDim_c", 32804, 38, false, null, 0.026263599999999998d),
            new LegacyHoleWizardField("ThreadDepth", "moCThreadData_c", 29995, 41, false, null, 0.007619999999999998d),
        }),
        new LegacyHoleWizardFamily((ushort)0x20D, (ushort)44, 0x69u, 8, 25, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 33724, 997, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 33724, 1019, true, "Screw Clearances", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 33724, 1055, true, "1/2", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0x2F0, (ushort)37, 0x69u, 8, 22, 4, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moSingleFaceRef_w", 32953, 446, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moSingleFaceRef_w", 32953, 468, true, "Screw Clearances", 0),
            new LegacyHoleWizardField("FastenerSize", "moSingleFaceRef_w", 32953, 504, true, "1/2", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0xF1, (ushort)47, 0x21u, 8, 48, 2, new LegacyHoleWizardField[]
        {
        }),
        new LegacyHoleWizardFamily((ushort)0x95, (ushort)22, 0x3Cu, 1666, 14, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 40553, 447, true, "MT&D Inch Mounting", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 40553, 487, true, "Drill&Cbore For SHCS (Shallow)", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 40553, 551, true, "1/4", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 19242, 10144, false, null, 0.0071374d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 19242, 10596, false, null, 0.01905d),
            new LegacyHoleWizardField("CounterBoreDiameter", "moAngleParameter_c", 19242, 11040, false, null, 0.010312400000000001d),
            new LegacyHoleWizardField("CounterBoreDepth", "moAngleParameter_c", 19242, 11486, false, null, 0.007112d),
        }),
        new LegacyHoleWizardFamily((ushort)0x1AA, (ushort)50, 0xC8u, 3536, 25, 2, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 33796, 1033, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 33796, 1055, true, "Screw Clearances", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 33796, 1091, true, "#6", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "sgAnglDim", 11336, 15114, false, null, 0.0037972999999999995d),
            new LegacyHoleWizardField("ThruHoleDepth", "sgAnglDim", 11336, 15566, false, null, 0.001290320000000001d),
        }),
        new LegacyHoleWizardFamily((ushort)0x72, (ushort)31, 0x13u, 1119, 0, 0, new LegacyHoleWizardField[]
        {
        }),
        new LegacyHoleWizardFamily((ushort)0x6F, (ushort)21, 0x23u, 513, 18, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 33250, 447, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 33250, 469, true, "Binding Head Screw", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 33250, 509, true, "#6", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 8389, 10144, false, null, 0.0037972999999999995d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 8389, 10596, false, null, 0.03d),
            new LegacyHoleWizardField("CounterBoreDiameter", "moAngleParameter_c", 8389, 11040, false, null, 0.0079375d),
            new LegacyHoleWizardField("CounterBoreDepth", "moAngleParameter_c", 8389, 11486, false, null, 0.0022097999999999996d),
        }),
        new LegacyHoleWizardFamily((ushort)0x7E, (ushort)47, 0x158u, 388, 14, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 13234, 1021, true, "ANSI Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 13234, 1043, true, "Binding Head Screw", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 13234, 1083, true, "#00", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0x6A, (ushort)24, 0xACu, 1026, 44, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 36591, 1005, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 36591, 1031, true, "Flat Head Screw - ANSI B18.6.7M", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 36591, 1097, true, "M3.5", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "sgAnglDim", 12121, 13410, false, null, 0.0039d),
            new LegacyHoleWizardField("ThruHoleDepth", "sgAnglDim", 12121, 13862, false, null, 0.005000000000000003d),
            new LegacyHoleWizardField("CounterSinkDiameter", "sgAnglDim", 12121, 14316, false, null, 0.008199999999999999d),
            new LegacyHoleWizardField("CounterSinkAngle", "sgAnglDim", 12121, 14772, false, null, 1.5707963267948966d),
        }),
        new LegacyHoleWizardFamily((ushort)0x6F, (ushort)24, 0xACu, 1026, 22, 0, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 31729, 985, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 31729, 1011, true, "Drill sizes", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 31729, 1037, true, "Ø7.0", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0xA0, (ushort)25, 0x38u, 524, 48, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("ThreadDiameter", "ThreeDdiameterDim_c", 38179, 38, false, null, 0.01d),
            new LegacyHoleWizardField("ThreadDepth", "sgAnglDim", 13734, 15586, false, null, 0.01905d),
        }),
        new LegacyHoleWizardFamily((ushort)0x101, (ushort)72, 0x6Au, 2554, 25, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 35809, 993, true, "ANSI Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 35809, 1015, true, "All Drill sizes", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 35809, 1049, true, "15/64", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0x88, (ushort)27, 0x41u, 8, 14, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 39026, 1005, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 39026, 1031, true, "Hex Bolt - ANSI B18.2.3.5M", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 39026, 1087, true, "M5", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 18395, 10144, false, null, 0.0055d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 18395, 10596, false, null, 0.028d),
            new LegacyHoleWizardField("CounterBoreDiameter", "moAngleParameter_c", 18395, 11040, false, null, 0.009000000000000001d),
            new LegacyHoleWizardField("CounterBoreDepth", "moAngleParameter_c", 18395, 11486, false, null, 0.006d),
        }),
        new LegacyHoleWizardFamily((ushort)0x92, (ushort)33, 0x66u, 8, 46, 0, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("ThreadDiameter", "ThreeDdiameterDim_c", 42088, 38, false, null, 0.008d),
            new LegacyHoleWizardField("ThreadDepth", "moCThreadData_c", 39084, 21, false, null, 0.0159999934d),
        }),
        new LegacyHoleWizardFamily((ushort)0x67, (ushort)23, 0x2Au, 8, 25, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 34486, 476, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 34486, 498, true, "Screw Clearances", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 34486, 534, true, "#12", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "sgAnglDim", 11457, 15114, false, null, 0.0057912d),
            new LegacyHoleWizardField("ThruHoleDepth", "sgAnglDim", 11457, 15566, false, null, 0.05436868468130723d),
        }),
        new LegacyHoleWizardFamily((ushort)0x71, (ushort)31, 0x5Au, 2091, 14, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 34154, 997, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 34154, 1023, true, "Socket Head Cap Screw", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 34154, 1069, true, "M10", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 13491, 10144, false, null, 0.011d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 13491, 10596, false, null, 0.01905d),
            new LegacyHoleWizardField("CounterBoreDiameter", "moAngleParameter_c", 13491, 11040, false, null, 0.018000000000000002d),
            new LegacyHoleWizardField("CounterBoreDepth", "moAngleParameter_c", 13491, 11486, false, null, 0.01d),
        }),
        new LegacyHoleWizardFamily((ushort)0x68, (ushort)26, 0x34u, 490, 25, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 31893, 447, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 31893, 473, true, "Tap Drills", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 31893, 497, true, "M10x1.0", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 8845, 15108, false, null, 0.009000000000000001d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 8845, 15560, false, null, 0.04445000000000001d),
        }),
        new LegacyHoleWizardFamily((ushort)0x6E, (ushort)23, 0x6Du, 1992, 52, 1, new LegacyHoleWizardField[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 39259, 463, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 39259, 489, true, "Tapped hole", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 39259, 515, true, "M10x1.25", 0),
            new LegacyHoleWizardField("ThreadDiameter", "moAngleParameter_c", 16203, 15114, false, null, 0.01d),
            new LegacyHoleWizardField("ThreadDepth", "moAngleParameter_c", 16203, 15576, false, null, 0.012000000000000009d),
        })
    };
}
