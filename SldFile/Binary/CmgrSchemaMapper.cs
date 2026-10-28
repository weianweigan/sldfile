using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class CmgrSchemaMapper
{
    public static void Map(byte[] data, CmgrData model)
    {
        model.RawData = data;
        var doc = MfcDocument.Parse(data);

        // Display name from moNodeName_c
        model.DisplayName = doc.FindFirst("moNodeName_c")?.AsWChar() ?? string.Empty;

        // Visual properties (starts at visProps.DataStart, spans 32 bytes of packed floats/uint)
        if (doc.FindFirst("moVisualProperties_c") is { } visProps)
        {
            var reader = new MfcReader(data, visProps.DataStart);
            model.VisualProperties = new CmgrVisualProperties
            {
                ColorRgb = reader.ReadU32(),
                Transparency = reader.ReadF32(),
                Ambient = reader.ReadF32(),
                Diffuse = reader.ReadF32(),
                Specular = reader.ReadF32(),
                Shininess = reader.ReadF32(),
                Emission = reader.ReadF32(),
                Blend = reader.ReadF32()
            };
        }

        // Hatch pattern (Part only)
        model.HatchPattern = doc.FindFirst("gcXhatch_c")?.AsWChar();

        // File paths and component names
        foreach (var element in doc.AllElements)
        {
            if (element.ClassName == "moCStringHandle_c")
            {
                var reader = element.CreateReader();
                var extRef = new CmgrExternalRef
                {
                    FullPath = reader.ReadWChar() ?? string.Empty
                };

                // Look for short name after the 0x17 0x80 marker
                for (int i = reader.Offset; i < element.DataEnd - 3; i++)
                {
                    if (data[i] == 0x17 && data[i + 1] == 0x80)
                    {
                        var shortReader = new MfcReader(data, i + 2, element.DataEnd - (i + 2));
                        extRef.ShortName = shortReader.ReadWChar() ?? string.Empty;
                        break;
                    }
                }

                model.FilePathsBuilder.Add(extRef);
            }
            else if (element.ClassName == "moExtFeature_c")
            {
                // Assembly component names through moCStringHandle_c children
                foreach (var child in doc.FindChildrenOf(element, "moCStringHandle_c"))
                {
                    var name = child.AsWChar();
                    if (!string.IsNullOrEmpty(name))
                        model.ComponentNamesBuilder.Add(name!);
                }
            }
        }

        model.Freeze();
    }
}
