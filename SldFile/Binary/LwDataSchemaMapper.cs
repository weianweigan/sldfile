using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class LwDataSchemaMapper
{
    public static void Map(byte[] data, LwData model)
    {
        model.RawData = data;
        var reader = new MfcReader(data);

        // Read type_flag prefix
        model.TypeFlag = reader.ReadU32();

        var doc = MfcDocument.Parse(data);

        // Extract boundary data between prefix and first MFC tag
        if (doc.AllElements.Count > 0 && doc.AllElements[0].TagOffset > reader.Offset)
        {
            int firstTagOffset = doc.AllElements[0].TagOffset;
            model.BodyTransformData = reader.ReadBytes(firstTagOffset - reader.Offset);
        }

        // Hatch pattern (gcXhatch_c) — Part only
        model.HatchPattern = doc.FindFirst("gcXhatch_c")?.AsWChar();

        // Plane nodes (moLWPlaneNodeData_c)
        foreach (var element in doc.AllElements)
        {
            if (element.ClassName == "moLWPlaneNodeData_c")
            {
                var nodeReader = element.CreateReader();
                var node = new LwPlaneNodeData
                {
                    PlaneClassName = nodeReader.ReadWChar() ?? string.Empty,
                    DataClassName = nodeReader.ReadWChar() ?? string.Empty,
                    FeatureIdentifier = nodeReader.ReadWChar() ?? string.Empty,
                    TransformMatrix = nodeReader.ReadRemainingBytes()
                };

                model.PlaneNodesBuilder.Add(node);
            }
        }

        model.Freeze();
    }
}
