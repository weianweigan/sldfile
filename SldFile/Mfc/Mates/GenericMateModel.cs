using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Fallback model for unmodeled or custom mate classes.
/// Preserves raw stream structure and payload losslessly.
/// </summary>
internal sealed class GenericMateModel : MfcMateModelBase
{
    private readonly string _className;

    public override string ClassName => _className;
    public override AssemblyMateType MateType => AssemblyMateType.Unknown;

    public byte[]? RawRecordBytes { get; set; }

    public GenericMateModel(string className)
    {
        _className = className ?? "moMateUnknown";
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        if (ar.IsLoading)
        {
            if (ar.Remaining > 0)
            {
                RawRecordBytes = ar.ReadRemainingBytes();
            }
        }
        else
        {
            if (RawRecordBytes != null && RawRecordBytes.Length > 0)
            {
                ar.WriteBytes(RawRecordBytes);
            }
        }
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new StandardMateDefinitionData(AssemblyMateType.Unknown)
        {
            DisplayName = DisplayName,
            Alignment = Alignment
        };
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        DisplayName = definition.DisplayName;
        Alignment = definition.Alignment;
    }
}
