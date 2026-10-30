using System;
using System.Collections.Generic;
using System.IO;
using OpenMcdf;

namespace SldFile.Ole;

internal static class StorageStreams
{
    internal static Dictionary<string, byte[]> ReadFile(string path)
    {
        using var root = RootStorage.OpenRead(path);
        return Read(root);
    }

    internal static Dictionary<string, byte[]> Read(Storage root)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        void Visit(Storage storage, string prefix)
        {
            foreach (var entry in storage.EnumerateEntries())
            {
                string path = prefix + entry.Name;
                if (entry.Type == EntryType.Storage)
                {
                    var child = storage.OpenStorage(entry.Name);
                    Visit(child, path + "/");
                }
                else if (entry.Type == EntryType.Stream)
                {
                    using var stream = storage.OpenStream(entry.Name);
                    var bytes = new byte[checked((int)stream.Length)];
                    int position = 0;
                    while (position < bytes.Length)
                    {
                        int count = stream.Read(bytes, position, bytes.Length - position);
                        if (count == 0) throw new EndOfStreamException($"Cannot read document data: {path}");
                        position += count;
                    }
                    result.Add(path, bytes);
                }
            }
        }
        Visit(root, "");
        return result;
    }

    internal static void Write(Storage root, string path, byte[] bytes)
    {
        var parts = path.Split('/');
        void WriteAt(Storage storage, int index)
        {
            if (index < parts.Length - 1)
            {
                var child = storage.OpenStorage(parts[index]);
                WriteAt(child, index + 1);
                return;
            }
            using var stream = storage.OpenStream(parts[index]);
            stream.Position = 0;
            stream.Write(bytes, 0, bytes.Length);
            stream.SetLength(bytes.Length);
        }
        WriteAt(root, 0);
    }

    internal static void WriteFile(string filePath, string path, byte[] bytes)
    {
        using var root = RootStorage.Open(filePath, FileMode.Open, FileAccess.ReadWrite, StorageModeFlags.Transacted);
        Write(root, path, bytes);
        root.Commit();
    }
}
