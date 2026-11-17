using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SldFile.Mfc;

// swccu.dll su_CArchive::ReadClass 0x31DAA2D0 and ReadObject 0x31DAA550.
// Classes and objects share one index space; null occupies index zero.
// Unlike a tag scanner, this reader never attempts to resynchronize inside a payload.
internal sealed class NativeDrawingObjectGraph
{
    internal sealed class Node
    {
        internal string ClassName = string.Empty;
        internal ushort Schema;
        internal IMfcSerializable Payload = null!;
    }

    private sealed class ClassEntry
    {
        internal string Name = string.Empty;
        internal ushort Schema;
    }

    private readonly Dictionary<uint, object> _entries = new Dictionary<uint, object>();
    private readonly Dictionary<Node, uint> _objects = new Dictionary<Node, uint>();
    private readonly Dictionary<string, uint> _classes = new Dictionary<string, uint>(StringComparer.Ordinal);
    private readonly Func<string, ushort, IMfcSerializable> _factory;
    private uint _next = 1;

    internal NativeDrawingObjectGraph(Func<string, ushort, IMfcSerializable> factory, uint initialIndex = 1)
    {
        if (initialIndex == 0 || initialIndex >= 0x80000000) throw new ArgumentOutOfRangeException(nameof(initialIndex));
        _factory = factory; _next = initialIndex;
    }

    // Map an already established parent-archive class when decoding a nested
    // segment. The caller must supply the actual prior index/schema; this is
    // not a mechanism for inferring a class from a failed reference tag.
    internal void MapExistingClass(uint index, string name, ushort schema)
    {
        if (index == 0 || index >= _next || string.IsNullOrEmpty(name)
            || _entries.ContainsKey(index) || _classes.ContainsKey(name))
            throw new InvalidDataException("Invalid existing native MFC class mapping.");
        _entries.Add(index, new ClassEntry { Name = name, Schema = schema });
        _classes.Add(name, index);
    }

    internal void Object(MfcArchive ar, ref Node? node)
    {
        ar.Context.NativeDrawingObjects = this;
        if (ar.IsLoading)
        {
            int tagPosition = ar.Position;
            uint tag = ReadTag(ar, out bool newClass);
            if (tag == 0) { node = null; return; }
            if (!newClass && (tag & 0x80000000) == 0)
            {
                if (!_entries.TryGetValue(tag, out var entry) || entry is not Node referenced)
                    throw new InvalidDataException($"Invalid native MFC object reference {tag} at {tagPosition}.");
                node = referenced;
                return;
            }
            ClassEntry descriptor;
            if (newClass)
            {
                ushort schema = 0, length = 0; ar.U16(ref schema); ar.U16(ref length);
                if (length == 0 || length > ar.Remaining) throw new InvalidDataException("Invalid native MFC class name.");
                string name = Encoding.ASCII.GetString(ar.ReadBytes(length));
                descriptor = new ClassEntry { Name = name, Schema = schema };
                _entries.Add(_next++, descriptor);
            }
            else
            {
                uint index = tag & 0x7FFFFFFF;
                if (!_entries.TryGetValue(index, out var entry) || entry is not ClassEntry known)
                    throw new InvalidDataException($"Invalid native MFC class reference {index} at {tagPosition}.");
                descriptor = known;
            }
            node = new Node { ClassName = descriptor.Name, Schema = descriptor.Schema,
                Payload = _factory(descriptor.Name, descriptor.Schema) };
            _entries.Add(_next++, node); // Register before loading nested objects, including cycles.
            node.Payload.Serialize(ar);
            return;
        }

        if (node == null) { WriteTag(ar, 0); return; }
        if (_objects.TryGetValue(node, out uint reference)) { WriteTag(ar, reference); return; }
        if (_classes.TryGetValue(node.ClassName, out uint classIndex))
        {
            var descriptor = (ClassEntry)_entries[classIndex];
            if (descriptor.Schema != node.Schema) throw new InvalidDataException("Conflicting native MFC schemas.");
            WriteTag(ar, classIndex | 0x80000000);
        }
        else
        {
            byte[] name = Encoding.ASCII.GetBytes(node.ClassName);
            if (name.Length == 0 || name.Length > ushort.MaxValue)
                throw new InvalidDataException("Invalid native MFC class name length.");
            ushort marker = ushort.MaxValue, length = (ushort)name.Length;
            ar.U16(ref marker); ar.U16(ref node.Schema); ar.U16(ref length); ar.WriteBytes(name);
            _classes.Add(node.ClassName, _next);
            _entries.Add(_next++, new ClassEntry { Name = node.ClassName, Schema = node.Schema });
        }
        _objects.Add(node, _next);
        _entries.Add(_next++, node);
        node.Payload.Serialize(ar);
    }

    private static uint ReadTag(MfcArchive ar, out bool newClass)
    {
        ushort word = 0; ar.U16(ref word);
        newClass = word == 0xFFFF;
        if (newClass) return 0xFFFFFFFF;
        if (word == 0x7FFF) { uint extended = 0; ar.U32(ref extended); return extended; }
        return (uint)(word & 0x7FFF) | ((word & 0x8000) != 0 ? 0x80000000u : 0);
    }

    private static void WriteTag(MfcArchive ar, uint value)
    {
        uint index = value & 0x7FFFFFFF;
        if (index < 0x7FFF)
        {
            ushort word = (ushort)(index | ((value & 0x80000000) != 0 ? 0x8000u : 0));
            ar.U16(ref word);
        }
        else { ushort marker = 0x7FFF; ar.U16(ref marker); ar.U32(ref value); }
    }
}

// sldmfcu.dll moLayerData_c::Serialize 0x3CB06A30.
internal sealed class NativeDrawingLayerDataModel : IMfcSerializable
{
    internal string? Name, Description;
    internal int Color, Style;
    internal NativeDrawingLineWidthModel LineWidth = new NativeDrawingLineWidthModel();

    public void Serialize(MfcArchive ar)
    {
        DrawingPreviewRecordModel.String(ar, ref Name);
        DrawingPreviewRecordModel.String(ar, ref Description);
        ar.I32(ref Color); ar.I32(ref Style);
        LineWidth.Serialize(ar);
    }
}
