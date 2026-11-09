using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for Contents/Definition binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes document file type, line style/fonts, user model env, and drawing unit system classes.
/// </summary>
internal sealed class DefinitionModel : IMfcSerializable
{
    public const string LineStyleClassName = "moLineStyle_c";
    public const string LineFontMgrClassName = "uiLineFontMgr_c";
    public const string LFConfigClassName = "uiLFConfig_c";
    public const string UserModelEnvClassName = "uiUserModelEnv_c";

    private static readonly string[] UnitClassNames =
    {
        "moLengthUserUnits_c", "moAngleUserUnits_c", "moNumberUserUnits_c",
        "moDensityUnits_c", "moFloatNumberUserUnits_c", "moSpringConstantUnits_c",
        "moUnitSysUnits_c", "moForceUnits_c", "moStressUnits_c",
        "moGravityUnits_c", "moLinearMotorUnits_c", "moRotaryMotorUnits_c",
        "moPowerUnits_c", "moEnergyUnits_c", "moTimeUnits_c", "moFrequencyUserUnits_c"
    };

    public DefinitionFileType FileType { get; set; } = DefinitionFileType.Unknown;
    public string? LineStyle { get; set; }
    public string? LineFont { get; set; }
    public string? LineFontConfig { get; set; }
    public string? UserName { get; set; }
    public List<string> UnitSystemClasses { get; } = new();

    public byte[]? RawData { get; set; }

    public DefinitionModel() { }



    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            // Storing from scratch
            uint ft = (uint)FileType;
            ar.U32(ref ft);

            if (FileType == DefinitionFileType.Part || FileType == DefinitionFileType.Assembly)
            {
                if (!string.IsNullOrEmpty(LineStyle))
                {
                    ushort tag = 0;
                    ar.WriteClassTag(LineStyleClassName, ref tag, schema: 1);
                    string? ls = LineStyle;
                    ar.WChar(ref ls);
                }

                if (!string.IsNullOrEmpty(LineFont))
                {
                    ushort tag = 0;
                    ar.WriteClassTag(LineFontMgrClassName, ref tag, schema: 1);
                    string? lf = LineFont;
                    ar.WChar(ref lf);
                }

                if (!string.IsNullOrEmpty(LineFontConfig))
                {
                    ushort tag = 0;
                    ar.WriteClassTag(LFConfigClassName, ref tag, schema: 1);
                    string? lfc = LineFontConfig;
                    ar.WChar(ref lfc);
                }

                if (!string.IsNullOrEmpty(UserName))
                {
                    ushort tag = 0;
                    ar.WriteClassTag(UserModelEnvClassName, ref tag, schema: 1);
                    string? un = UserName;
                    ar.WChar(ref un);
                }
            }
            else if (FileType == DefinitionFileType.Drawing)
            {
                foreach (var unitName in UnitSystemClasses)
                {
                    ushort tag = 0;
                    ar.WriteClassTag(unitName, ref tag, schema: 1);
                }
            }
        }
        else
        {
            UnitSystemClasses.Clear();
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var reader = new MfcReader(data);
            if (reader.TryReadU32(out uint firstVal) && Enum.IsDefined(typeof(DefinitionFileType), (int)firstVal))
            {
                FileType = (DefinitionFileType)firstVal;
            }

            var doc = MfcDocument.Parse(data);

            if (FileType == DefinitionFileType.Part || FileType == DefinitionFileType.Assembly)
            {
                LineStyle = doc.FindFirst(LineStyleClassName)?.AsWChar();
                LineFont = doc.FindFirst(LineFontMgrClassName)?.AsWChar();
                LineFontConfig = doc.FindFirst(LFConfigClassName)?.AsWChar();
                UserName = doc.FindFirst(UserModelEnvClassName)?.AsWChar();
            }

            if (FileType == DefinitionFileType.Drawing)
            {
                foreach (var element in doc.AllElements)
                {
                    if (element.ClassName != null)
                    {
                        foreach (var unitName in UnitClassNames)
                        {
                            if (element.ClassName == unitName)
                            {
                                UnitSystemClasses.Add(unitName);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}

internal enum DefinitionFileType
{
    Unknown = 0,
    Part = 0x51,
    Assembly = 0x68,
    Drawing = 0x50
}