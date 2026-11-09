using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for Contents/Config-0-MatesList binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes stream version, mate count, and strongly-typed IMfcMateModel instances.
/// </summary>
internal sealed class MatesListModel : IMfcSerializable
{
    public const ushort ExpectedVersion = 0x0004;
    public const ushort DefaultCleanVersion = 0x00AB;
    public const ushort Legacy2012Version = 0x0004;

    public ushort StreamVersion { get; set; } = DefaultCleanVersion;
    public List<IMfcMateModel> Mates { get; } = new();

    public MatesListModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            // 1. Write StreamVersion
            ushort ver = StreamVersion;
            ar.U16(ref ver);

            // 2. SW2026 header: Reserved (0x0000) and MateCount (0x0001+)
            if (ver >= DefaultCleanVersion)
            {
                ushort reserved = 0;
                ar.U16(ref reserved);

                ushort count = (ushort)Mates.Count;
                ar.U16(ref count);
            }

            // 3. Serialize each mate model symmetrically
            foreach (var mate in Mates)
            {
                mate.Serialize(ar);
            }
        }
        else
        {
            Mates.Clear();
            if (ar.Remaining < 2) return;

            byte[] data = ar.ReadRemainingBytes();
            if (data == null || data.Length < 2) return;

            StreamVersion = BitConverter.ToUInt16(data, 0);

            if (TryDeserializePureArchive(data))
            {
                return;
            }

            DeserializeFromNativeDocument(data);
        }
    }

    private bool TryDeserializePureArchive(byte[] data)
    {
        using var archive = MfcArchive.CreateReader(data);
        ushort ver = 0;
        archive.U16(ref ver);

        ushort reserved = 0;
        ushort count = 0;
        bool hasCount = false;
        if (ver >= DefaultCleanVersion)
        {
            if (archive.Remaining < 4) return false;
            archive.U16(ref reserved);
            archive.U16(ref count);
            hasCount = true;
        }

        var mates = new List<IMfcMateModel>();
        int targetCount = hasCount ? count : int.MaxValue;

        while (archive.Remaining >= 2 && mates.Count < targetCount)
        {
            var kind = archive.ReadClassTag(out string className, out ushort classRefTag, out ushort schema);
            if (archive.Context.HasNativeExplicitTags)
                return false;

            if (kind == MfcTagKind.Invalid || kind == MfcTagKind.Null || string.IsNullOrEmpty(className))
                return false;

            if (!MfcMateRegistry.IsMateClass(className))
                return false;

            var mate = MfcMateRegistry.Create(className);
            try
            {
                mate.DeserializePayload(archive);
                mates.Add(mate);
            }
            catch
            {
                return false;
            }
        }

        if (archive.Context.HasNativeExplicitTags)
            return false;

        if (hasCount && mates.Count == count && archive.Remaining == 0)
        {
            Mates.AddRange(mates);
            return true;
        }

        if (!hasCount && mates.Count > 0 && archive.Remaining == 0)
        {
            Mates.AddRange(mates);
            return true;
        }

        return false;
    }

    private void DeserializeFromNativeDocument(byte[] data)
    {
        var doc = MfcDocument.Parse(data);
        var parsed = new List<(int Offset, IMfcMateModel Mate)>();

        foreach (var element in doc.AllElements)
        {
            if (!MfcMateRegistry.IsMateClass(element.ClassName))
                continue;

            var mate = MfcMateRegistry.Create(element.ClassName!);
            mate.InitializeFromElement(element, data);
            parsed.Add((element.TagOffset, mate));
        }

        foreach (var refEntry in MatesListClassRefParser.Scan(data, doc))
        {
            var mate = MfcMateRegistry.Create(refEntry.Entry.ClassName);
            mate.InitializeFromMateEntry(refEntry.Entry, data);
            parsed.Add((refEntry.Offset, mate));
        }

        parsed.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        foreach (var item in parsed)
            Mates.Add(item.Mate);
    }
}

internal sealed class MateEntry
{
    public string ClassName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public uint TypeId { get; set; }
    public ushort MateFlags { get; set; }
    public double GapValue { get; set; }
    public IReadOnlyList<MateReferenceEntry> References { get; internal set; } = Array.Empty<MateReferenceEntry>();
    public MateStructureData? Structure { get; internal set; }
    public byte[]? RawData { get; set; }
}

internal sealed class MateReferenceEntry
{
    public string WrapperClassName { get; set; } = string.Empty;
    public IReadOnlyList<string> EntityClassNames { get; set; } = Array.Empty<string>();
}