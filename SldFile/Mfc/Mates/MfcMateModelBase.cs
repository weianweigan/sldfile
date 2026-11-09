using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Base class implementing the standard MFC CArchive framing and header structure for mate models.
/// </summary>
internal abstract class MfcMateModelBase : IMfcMateModel
{
    public const ushort DefaultSchema = 0x0001;
    public const int ControlledHeaderLength = 174;

    public abstract string ClassName { get; }
    public abstract AssemblyMateType MateType { get; }
    public virtual uint TypeId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public AssemblyMateAlignment Alignment { get; set; } = AssemblyMateAlignment.Aligned;
    public ushort MateFlags { get; set; }

    /// <summary>Preserved header payload bytes for lossless roundtrip when header fields are constant.</summary>
    public byte[]? HeaderPayload { get; set; }

    protected readonly List<AssemblyMateEntityReference> _entities = new();
    public IReadOnlyList<AssemblyMateEntityReference> EntityReferences => _entities;

    protected readonly List<MateReferenceEntry> _rawReferences = new();
    protected MateStructureData? _decodedStructure;
    protected bool _wasLoadedFromStream;
    protected bool _wasLoadedFromElement;

    public MateStructureData? Structure => _decodedStructure;
    public IReadOnlyList<MateReferenceEntry> References => _rawReferences;

    public virtual void AddPlaneReference(string selectionName)
    {
        if (string.IsNullOrEmpty(selectionName)) return;
        string component = string.Empty;
        string feature = string.Empty;
        var parts = selectionName.Split('@');
        if (parts.Length > 0) feature = parts[0];
        if (parts.Length > 1) component = parts[1];
        _entities.Add(new AssemblyMateEntityReference(
            MateReferenceKind.Plane,
            component,
            feature,
            selectionName,
            null));
    }

    public virtual void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort classTag = 0;
            ar.ClassTagOrRef(ClassName, ref classTag);
            SerializePayload(ar);
        }
        else
        {
            // If class tag is present at current position (standalone deserialization), consume it.
            if (ar.Remaining >= 2)
            {
                int peekPos = ar.Position;
                ushort tag = 0;
                ar.U16(ref tag);
                ar.Position = peekPos;

                if (tag == 0xFFFF || ar.Context.TryResolveClassReference(tag, out _))
                {
                    ushort classTag = 0;
                    ar.ClassTagOrRef(ClassName, ref classTag);
                }
            }
            DeserializePayload(ar);
        }
    }

    public virtual void SerializePayload(MfcArchive ar)
    {
        string? name = DisplayName;
        ar.WChar(ref name);

        // 2-byte word alignment so headerStart is bit-exact with SW2026 wire format
        ushort postNameWord = 0;
        ar.U16(ref postNameWord);

        SerializeHeader(ar);
        SerializeEntities(ar);
        SerializeParameters(ar);
    }

    public virtual void DeserializePayload(MfcArchive ar)
    {
        string? name = DisplayName;
        ar.WChar(ref name);
        if (name != null)
            DisplayName = name;

        ushort postNameWord = 0;
        ar.U16(ref postNameWord);

        SerializeHeader(ar);
        SerializeEntities(ar);
        SerializeParameters(ar);

        EnsureStructurePopulated();
    }

    protected void EnsureStructurePopulated()
    {
        var structData = new MateStructureData
        {
            ClassName = ClassName,
            DisplayName = DisplayName,
            MateTypeId = TypeId,
            AlignmentId = Alignment switch
            {
                AssemblyMateAlignment.Aligned => 0u,
                AssemblyMateAlignment.AntiAligned => 1u,
                AssemblyMateAlignment.Closest => 2u,
                _ => null
            }
        };

        PopulateStructureData(structData);
        _decodedStructure = structData;
    }

    protected virtual void SerializeHeader(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (HeaderPayload != null && HeaderPayload.Length == ControlledHeaderLength)
            {
                // Patch alignment byte into controlled header (+0xA1)
                byte alignByte = Alignment switch
                {
                    AssemblyMateAlignment.Aligned => 0x01,
                    AssemblyMateAlignment.AntiAligned => 0x02,
                    AssemblyMateAlignment.Closest => 0x00,
                    _ => 0x01
                };
                HeaderPayload[0xA1] = alignByte;
                ar.WriteBytes(HeaderPayload);
            }
            else
            {
                // Construct standard 174-byte controlled 2026 header
                byte[] header = new byte[ControlledHeaderLength];
                header[0x05] = 0x40; // 0x4000
                header[0x06] = 0x01; // Serial 1
                // Empty WChar at +0x0E: FF FE FF 00
                header[0x0E] = 0xFF; header[0x0F] = 0xFE; header[0x10] = 0xFF; header[0x11] = 0x00;
                // Alignment at +0xA1
                header[0xA1] = Alignment switch
                {
                    AssemblyMateAlignment.Aligned => 0x01,
                    AssemblyMateAlignment.AntiAligned => 0x02,
                    AssemblyMateAlignment.Closest => 0x00,
                    _ => 0x01
                };
                // Fixed marker pattern at +0xA2..+0xAD
                header[0xA3] = 0x01; header[0xA6] = 0x02;
                ar.WriteBytes(header);
            }
        }
        else
        {
            if (ar.Remaining >= ControlledHeaderLength)
            {
                HeaderPayload = ar.ReadBytes(ControlledHeaderLength);
                byte mode = HeaderPayload[0xA1];
                Alignment = mode switch
                {
                    0x01 => AssemblyMateAlignment.Aligned,
                    0x02 => AssemblyMateAlignment.AntiAligned,
                    0x00 => AssemblyMateAlignment.Closest,
                    _ => AssemblyMateAlignment.Aligned
                };
            }
        }
    }

    protected abstract void SerializeEntities(MfcArchive ar);
    protected abstract void SerializeParameters(MfcArchive ar);

    public abstract AssemblyMateDefinitionData ToDefinition();
    public abstract void FromDefinition(AssemblyMateDefinitionData definition);

    public virtual MateEntry ToMateEntry()
    {
        var entry = new MateEntry
        {
            ClassName = ClassName,
            DisplayName = DisplayName,
            TypeId = TypeId,
            MateFlags = MateFlags,
        };

        if (_decodedStructure != null)
        {
            entry.Structure = _decodedStructure;
            entry.References = _rawReferences.AsReadOnly();
            return entry;
        }

        if (_wasLoadedFromElement)
        {
            entry.Structure = null;
            entry.References = _rawReferences.AsReadOnly();
            return entry;
        }

        var structData = new MateStructureData
        {
            ClassName = ClassName,
            DisplayName = DisplayName,
            MateTypeId = TypeId,
            AlignmentId = Alignment switch
            {
                AssemblyMateAlignment.Aligned => 0u,
                AssemblyMateAlignment.AntiAligned => 1u,
                AssemblyMateAlignment.Closest => 2u,
                _ => null
            }
        };

        PopulateStructureData(structData);
        entry.Structure = structData;

        var refs = new List<MateReferenceEntry>(_rawReferences);
        PopulateReferences(refs);
        entry.References = refs.AsReadOnly();

        return entry;
    }

    protected virtual void PopulateStructureData(MateStructureData data) { }
    protected virtual void PopulateReferences(List<MateReferenceEntry> refs) { }

    public virtual void InitializeFromElement(MfcElement element, byte[] sourceData)
    {
        _wasLoadedFromStream = true;
        _wasLoadedFromElement = true;
        DisplayName = element.AsWChar() ?? string.Empty;
        var reader = element.CreateReader();
        reader.ReadWChar(); // Skip display name
        if (reader.Remaining >= 6)
        {
            TypeId = reader.ReadU32();
            MateFlags = reader.ReadU16();
        }

        _rawReferences.Clear();
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

            _rawReferences.Add(new MateReferenceEntry
            {
                WrapperClassName = child.ClassName,
                EntityClassNames = entityClasses.AsReadOnly()
            });
        }

        _decodedStructure = MateStructureDecoder.Decode(element);
        if (_decodedStructure != null)
        {
            PopulateFromDecodedStructure(_decodedStructure);
        }
    }

    public virtual void InitializeFromMateEntry(MateEntry entry, byte[] sourceData)
    {
        _wasLoadedFromStream = true;
        DisplayName = entry.DisplayName;
        TypeId = entry.TypeId;
        MateFlags = entry.MateFlags;
        _rawReferences.Clear();
        _rawReferences.AddRange(entry.References);
        _decodedStructure = entry.Structure;
        if (_decodedStructure != null)
        {
            PopulateFromDecodedStructure(_decodedStructure);
        }
    }

    protected virtual void PopulateFromDecodedStructure(MateStructureData structure)
    {
        if (structure.AlignmentId.HasValue)
        {
            Alignment = structure.AlignmentId.Value switch
            {
                0u => AssemblyMateAlignment.Aligned,
                1u => AssemblyMateAlignment.AntiAligned,
                2u => AssemblyMateAlignment.Closest,
                _ => AssemblyMateAlignment.Aligned
            };
        }

        _entities.Clear();
        foreach (var entity in structure.EntityReferences)
        {
            MateReferenceKind kind = entity.Kind switch
            {
                MateReferenceEntityKind.Cylinder => MateReferenceKind.Cylinder,
                MateReferenceEntityKind.Plane => MateReferenceKind.Plane,
                MateReferenceEntityKind.Edge => MateReferenceKind.Edge,
                MateReferenceEntityKind.Circle => MateReferenceKind.Circle,
                MateReferenceEntityKind.Path => MateReferenceKind.Path,
                MateReferenceEntityKind.SketchEntity => MateReferenceKind.Point,
                _ => MateReferenceKind.Unknown
            };
            _entities.Add(new AssemblyMateEntityReference(
                kind,
                entity.ComponentInstanceName,
                entity.FeatureName,
                entity.SelectionName,
                entity.PersistentId));
        }
    }
}
