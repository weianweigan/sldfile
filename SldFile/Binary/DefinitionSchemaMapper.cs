using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class DefinitionSchemaMapper
{
    // Unit system class names found in Drawing definitions
    private static readonly string[] UnitClassNames =
    {
        "moLengthUserUnits_c", "moAngleUserUnits_c", "moNumberUserUnits_c",
        "moDensityUnits_c", "moFloatNumberUserUnits_c", "moSpringConstantUnits_c",
        "moUnitSysUnits_c", "moForceUnits_c", "moStressUnits_c",
        "moGravityUnits_c", "moLinearMotorUnits_c", "moRotaryMotorUnits_c",
        "moPowerUnits_c", "moEnergyUnits_c", "moTimeUnits_c", "moFrequencyUserUnits_c"
    };

    public static void Map(byte[] data, DefinitionData model)
    {
        model.RawData = data;

        // Determine file type from first u32
        var reader = new MfcReader(data);
        if (reader.TryReadU32(out uint firstVal) && Enum.IsDefined(typeof(DefinitionFileType), (int)firstVal))
        {
            model.FileType = (DefinitionFileType)firstVal;
        }

        var doc = MfcDocument.Parse(data);

        // Part/Assembly: extract styling and user info from descendant elements
        if (model.FileType == DefinitionFileType.Part || model.FileType == DefinitionFileType.Assembly)
        {
            model.LineStyle = doc.FindFirst("moLineStyle_c")?.AsWChar();
            model.LineFont = doc.FindFirst("uiLineFontMgr_c")?.AsWChar();
            model.LineFontConfig = doc.FindFirst("uiLFConfig_c")?.AsWChar();
            model.UserName = doc.FindFirst("uiUserModelEnv_c")?.AsWChar();
        }

        // Drawing: collect unit system class names
        if (model.FileType == DefinitionFileType.Drawing)
        {
            foreach (var element in doc.AllElements)
            {
                if (element.ClassName != null)
                {
                    foreach (var unitName in UnitClassNames)
                    {
                        if (element.ClassName == unitName)
                        {
                            model.UnitSystemClassesBuilder.Add(unitName);
                            break;
                        }
                    }
                }
            }
        }

        model.Freeze();
    }
}
