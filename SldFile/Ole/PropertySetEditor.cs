using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SldFile.Ole;

/// <summary>
/// Edits individual properties while retaining unknown value blobs and dictionary aliases.
/// Unlike the exploratory serializer, this never decodes and re-encodes every value.
/// </summary>
internal sealed class PropertySetEditor
{
    private readonly byte[] _original;
    private readonly List<Section> _sections = new List<Section>();
    private readonly Dictionary<Section, byte[]> _padding = new Dictionary<Section, byte[]>();
    private readonly byte[] _trailing;
    private static readonly Guid UserPropertiesId = new Guid("D5CDD505-2E9C-101B-9397-08002B2CF9AE");

    internal PropertySetEditor(byte[] bytes)
    {
        _original = bytes;
        if (bytes.Length < 48 || bytes[0] != 0xFE || bytes[1] != 0xFF)
            throw new InvalidDataException("Invalid property set header.");
        int count = BitConverter.ToInt32(bytes, 24);
        if (count < 1 || count > 2 || bytes.Length < 28 + count * 20)
            throw new InvalidDataException("Unsupported property set sections.");
        for (int i = 0; i < count; i++)
        {
            int descriptor = 28 + i * 20;
            var guid = new byte[16];
            Array.Copy(bytes, descriptor, guid, 0, 16);
            int offset = BitConverter.ToInt32(bytes, descriptor + 16);
            if (offset < 28 + count * 20 || offset > bytes.Length - 8)
                throw new InvalidDataException("Invalid property section offset.");
            int length = BitConverter.ToInt32(bytes, offset);
            if (length < 8 || length > bytes.Length - offset)
                throw new InvalidDataException("Truncated property section.");
            _sections.Add(new Section(new Guid(guid), bytes.Skip(offset).Take(length).ToArray()) { OriginalOffset = offset });
        }
        int cursor = 28 + count * 20;
        foreach (var section in _sections.OrderBy(s => s.OriginalOffset))
        {
            if (section.OriginalOffset < cursor) throw new InvalidDataException("Overlapping property sections.");
            _padding[section] = bytes.Skip(cursor).Take(section.OriginalOffset - cursor).ToArray();
            cursor = section.OriginalOffset + section.OriginalLength;
        }
        _trailing = bytes.Skip(cursor).ToArray();
    }

    internal object? GetNamed(string name)
    {
        foreach (var section in _sections)
        {
            var match = section.ReadNames().FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match != null && section.Values.TryGetValue(match.Id, out var blob))
                return Decode(blob);
        }
        return null;
    }

    internal object? Get(uint id, bool custom = false)
    {
        var section = FindSection(custom);
        return section != null && section.Values.TryGetValue(id, out var blob) ? Decode(blob) : null;
    }

    internal void ReadCustom(PropertyCollection target)
    {
        var section = FindSection(true);
        if (section == null) return;
        foreach (var name in section.ReadNames())
        {
            var value = Get(name.Id, true);
            if (!string.IsNullOrEmpty(name.Name) && value != null) target.Load(name.Name, value);
        }
    }

    internal void Set(uint id, object? value)
    {
        var section = _sections[0];
        if (value == null) section.Values.Remove(id);
        else section.Values[id] = Encode(value);
        section.Changed = true;
    }

    internal void ApplyCustom(PropertyCollection properties)
    {
        if (properties.Changes.Count == 0) return;
        var section = FindSection(true);
        if (section == null)
        {
            section = new Section(UserPropertiesId);
            _sections.Add(section);
        }
        var names = section.ReadNames();
        foreach (var name in properties.Changes)
        {
            var matches = names.Where(n => n.Name == name).ToList();
            if (matches.Any(n => n.Id < 0x01000000 && section.Values.ContainsKey(0x01000000 + n.Id)))
                throw new NotSupportedException("Editing custom properties with associated linked values is not supported.");
            if (!properties.TryGetValue(name, out var value))
            {
                foreach (var match in matches) section.Values.Remove(match.Id);
                names.RemoveAll(n => n.Name == name);
                continue;
            }
            uint id;
            if (matches.Count > 0) id = matches[0].Id;
            else
            {
                id = checked(Math.Max(1u, section.Values.Keys.Concat(names.Select(n => n.Id)).Where(n => n < 0x01000000).DefaultIfEmpty(1u).Max()) + 1);
                if (id >= 0x01000000) throw new NotSupportedException("The custom property identifier range is exhausted.");
                names.Add(new PropertyName(id, name, null));
            }
            section.Values[id] = Encode(value);
        }
        section.WriteNames(names);
        section.Changed = true;
    }

    internal byte[] ToBytes()
    {
        if (_sections.All(s => !s.Changed)) return _original;
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write(_original, 0, 24);
        writer.Write(_sections.Count);
        writer.Write(new byte[20 * _sections.Count]);
        var offsets = new Dictionary<Section, int>();
        foreach (var section in _sections.Where(s => s.OriginalOffset >= 0).OrderBy(s => s.OriginalOffset))
        {
            writer.Write(_padding[section]);
            offsets[section] = checked((int)output.Position);
            writer.Write(section.Serialize());
        }
        writer.Write(_trailing);
        foreach (var section in _sections.Where(s => s.OriginalOffset < 0))
        {
            offsets[section] = checked((int)output.Position);
            writer.Write(section.Serialize());
        }
        output.Position = 28;
        foreach (var section in _sections)
        {
            writer.Write(section.Id.ToByteArray());
            writer.Write(offsets[section]);
        }
        return output.ToArray();
    }

    private Section? FindSection(bool custom) => custom
        ? _sections.FirstOrDefault(s => s.Id == UserPropertiesId) : _sections[0];

    internal static object? Decode(byte[] blob)
    {
        if (blob.Length < 8) return null;
        int type = BitConverter.ToInt32(blob, 0);
        switch (type)
        {
            case 2: return (int)BitConverter.ToInt16(blob, 4);
            case 3: return BitConverter.ToInt32(blob, 4);
            case 5: return blob.Length >= 12 ? (object)BitConverter.ToDouble(blob, 4) : null;
            case 11: return BitConverter.ToInt16(blob, 4) != 0;
            case 30:
            case 31:
                int count = BitConverter.ToInt32(blob, 4);
                int size = type == 31 ? checked(count * 2) : count;
                if (size <= 0 || size > blob.Length - 8) return null;
                return (type == 31 ? Encoding.Unicode : Encoding.UTF8).GetString(blob, 8, size).TrimEnd('\0');
            case 64:
                if (blob.Length < 12) return null;
                long time = BitConverter.ToInt64(blob, 4);
                return time == 0 ? null : (object)DateTime.FromFileTimeUtc(time);
            default: return null;
        }
    }

    internal static byte[] Encode(object value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        switch (value)
        {
            case string text:
                var bytes = Encoding.UTF8.GetBytes(text + "\0");
                writer.Write(30); writer.Write(bytes.Length); writer.Write(bytes); break;
            case int number: writer.Write(3); writer.Write(number); break;
            case double number: writer.Write(5); writer.Write(number); break;
            case bool boolean: writer.Write(11); writer.Write(boolean ? -1 : 0); break;
            case DateTime date: writer.Write(64); writer.Write(date.ToUniversalTime().ToFileTimeUtc()); break;
            default: throw new ArgumentException("Unsupported property value.", nameof(value));
        }
        return stream.ToArray();
    }

    private sealed class PropertyName
    {
        internal uint Id;
        internal string Name;
        internal byte[]? Raw;
        internal PropertyName(uint id, string name, byte[]? raw) { Id = id; Name = name; Raw = raw; }
    }

    private sealed class Section
    {
        internal readonly Guid Id;
        internal readonly Dictionary<uint, byte[]> Values = new Dictionary<uint, byte[]>();
        internal bool Changed;
        internal int OriginalOffset = -1;
        internal int OriginalLength => _raw?.Length ?? 0;
        private readonly byte[]? _raw;
        private readonly byte[] _valuePrefix = Array.Empty<byte>();
        private int CodePage => Values.TryGetValue(1, out var cp) && cp.Length >= 6 ? BitConverter.ToUInt16(cp, 4) : 65001;

        internal Section(Guid id)
        {
            Id = id;
            Values[0] = new byte[4];
            Values[1] = new byte[] { 2, 0, 0, 0, 233, 253, 0, 0 };
        }

        internal Section(Guid id, byte[] raw)
        {
            Id = id;
            _raw = raw;
            int count = BitConverter.ToInt32(raw, 4);
            if (count < 0 || count > (raw.Length - 8) / 8) throw new InvalidDataException("Invalid property table.");
            var offsets = new List<(uint Id, int Offset)>();
            for (int i = 0; i < count; i++)
            {
                int at = 8 + i * 8;
                int offset = BitConverter.ToInt32(raw, at + 4);
                if (offset < 8 + count * 8 || offset >= raw.Length) throw new InvalidDataException("Invalid property value offset.");
                offsets.Add((BitConverter.ToUInt32(raw, at), offset));
            }
            foreach (var entry in offsets)
            {
                int end = offsets.Where(e => e.Offset > entry.Offset).Select(e => e.Offset).DefaultIfEmpty(raw.Length).Min();
                if (Values.ContainsKey(entry.Id)) throw new InvalidDataException("Duplicate property table ID.");
                Values[entry.Id] = raw.Skip(entry.Offset).Take(end - entry.Offset).ToArray();
            }
            int tableEnd = 8 + count * 8;
            int firstValue = offsets.Select(e => e.Offset).DefaultIfEmpty(raw.Length).Min();
            _valuePrefix = raw.Skip(tableEnd).Take(firstValue - tableEnd).ToArray();
        }

        internal List<PropertyName> ReadNames()
        {
            var names = new List<PropertyName>();
            if (!Values.TryGetValue(0, out var bytes) || bytes.Length < 4) return names;
            int count = BitConverter.ToInt32(bytes, 0);
            int cursor = 4;
            if (count < 0 || count > (bytes.Length - 4) / 8) throw new InvalidDataException("Invalid property dictionary.");
            for (int i = 0; i < count; i++)
            {
                if (cursor > bytes.Length - 8) throw new InvalidDataException("Truncated property dictionary.");
                uint id = BitConverter.ToUInt32(bytes, cursor);
                int length = BitConverter.ToInt32(bytes, cursor + 4);
                if (length < 0 || length > bytes.Length - cursor - 8) throw new InvalidDataException("Invalid property name length.");
                string name = (CodePage == 1200 ? Encoding.Unicode : Encoding.UTF8).GetString(bytes, cursor + 8, length).TrimEnd('\0');
                names.Add(new PropertyName(id, name, bytes.Skip(cursor).Take(8 + length).ToArray()));
                cursor += 8 + length;
            }
            return names;
        }

        internal void WriteNames(List<PropertyName> names)
        {
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(names.Count);
            foreach (var name in names)
            {
                if (name.Raw != null) { writer.Write(name.Raw); continue; }
                var bytes = (CodePage == 1200 ? Encoding.Unicode : Encoding.UTF8).GetBytes(name.Name + "\0");
                writer.Write(name.Id); writer.Write(bytes.Length); writer.Write(bytes);
            }
            Values[0] = output.ToArray();
        }

        internal byte[] Serialize()
        {
            if (!Changed && _raw != null) return _raw;
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(0);
            writer.Write(Values.Count);
            int offset = 8 + Values.Count * 8 + _valuePrefix.Length;
            foreach (var pair in Values)
            {
                writer.Write(pair.Key); writer.Write(offset);
                offset = checked(offset + ((pair.Value.Length + 3) & ~3));
            }
            writer.Write(_valuePrefix);
            foreach (var pair in Values)
            {
                writer.Write(pair.Value);
                while ((output.Position & 3) != 0) writer.Write((byte)0);
            }
            output.Position = 0;
            writer.Write(checked((int)output.Length));
            return output.ToArray();
        }
    }
}
