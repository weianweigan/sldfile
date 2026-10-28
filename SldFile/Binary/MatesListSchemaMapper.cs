using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class MatesListSchemaMapper
{
    private static readonly string[] MateClassNames =
    {
        "moMateCoordinateM", "moMateCoincidentM",
        "moMateCoordinate_cM", "moMateCoincident_cM"
    };

    public static void Map(byte[] data, MatesListData model)
    {
        model.RawData = data;
        var streamReader = new MfcReader(data);

        // Read stream version
        model.StreamVersion = streamReader.ReadU16();

        // Parse MFC document to find mate entries
        var doc = MfcDocument.Parse(data);

        foreach (var element in doc.AllElements)
        {
            if (!IsMateClass(element.ClassName))
                continue;

            var reader = element.CreateReader();
            var mate = new MateEntry
            {
                ClassName = element.ClassName ?? string.Empty,
                RawData = element.RawData,
                DisplayName = reader.ReadWChar() ?? string.Empty,
                TypeId = reader.ReadU32(),
                MateFlags = reader.ReadU16()
            };

            // Look for small f64 gap value within the remaining element data
            for (int i = reader.Offset; i <= element.DataEnd - 8; i += 4)
            {
                double possibleGap = BitConverter.ToDouble(data, i);
                if (possibleGap > 0 && possibleGap < 10.0 && possibleGap != 1.0)
                {
                    mate.GapValue = possibleGap;
                    break;
                }
            }

            model.MatesBuilder.Add(mate);
        }

        model.Freeze();
    }

    private static bool IsMateClass(string? className)
    {
        if (string.IsNullOrEmpty(className) || className == null)
            return false;
        foreach (var name in MateClassNames)
        {
            if (className == name || className.StartsWith("moMate", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
