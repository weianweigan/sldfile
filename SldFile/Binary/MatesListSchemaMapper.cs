using System;
using System.Collections.Generic;
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

        var parsed = new List<(int Offset, MateEntry Entry)>();
        foreach (var element in doc.AllElements)
        {
            if (!IsMateClass(element.ClassName))
                continue;

            var reader = element.CreateReader();
            // Keep the legacy cursor position for TypeId / MateFlags / the gap
            // probe below; the display name is now read offset-tolerantly.
            reader.ReadWChar();
            var mate = new MateEntry
            {
                ClassName = element.ClassName ?? string.Empty,
                RawData = element.RawData,
                // 2012 stores the display-name WChar at DataStart; the 2026
                // layout prefixes it with two bytes (06 80). Scan the bounded
                // element head for the documented WChar prefix instead of
                // assuming one fixed offset.
                DisplayName = element.AsWChar() ?? string.Empty,
                TypeId = reader.ReadU32(),
                MateFlags = reader.ReadU16()
            };

            // Direct reference wrappers are the only mate structure currently
            // established by samples. Nested payloads stay undecoded; unknown
            // wrapper classes are preserved by name and classified as Unknown
            // by the public model instead of being guessed.
            var references = new List<MateReferenceEntry>();
            foreach (var child in element.Children)
            {
                if (string.IsNullOrEmpty(child.ClassName) ||
                    !child.ClassName.EndsWith("_w", StringComparison.Ordinal))
                    continue;

                var entityClasses = new List<string>();
                foreach (var entityChild in child.Children)
                {
                    if (!string.IsNullOrEmpty(entityChild.ClassName))
                        entityClasses.Add(entityChild.ClassName);
                }

                references.Add(new MateReferenceEntry
                {
                    WrapperClassName = child.ClassName,
                    EntityClassNames = entityClasses.AsReadOnly()
                });
            }
            mate.References = references.AsReadOnly();

            // Structural decode only: header candidate fields, reference chains and
            // deferred 2026 texts. Unknown layouts leave Structure null; public
            // mate type/alignment/entity/parameter flags stay false.
            mate.Structure = MateStructureDecoder.Decode(element);

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

            parsed.Add((element.TagOffset, mate));
        }

        // Repeated mate classes are serialized with an MFC class reference instead
        // of an inline tag; recover the structurally validated ones and keep the
        // public list in stream order.
        parsed.AddRange(MatesListClassRefParser.Scan(data, doc));
        parsed.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        foreach (var item in parsed)
            model.MatesBuilder.Add(item.Entry);

        model.Freeze();
    }

    private static bool IsMateClass(string? className)
    {
        if (string.IsNullOrEmpty(className) || className == null)
            return false;
        // "moMaterial*" also starts with "moMate"; never treat material
        // classes as mates if a MatesList stream ever contains one.
        if (className.StartsWith("moMaterial", StringComparison.Ordinal))
            return false;
        foreach (var name in MateClassNames)
        {
            if (className == name || className.StartsWith("moMate", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
