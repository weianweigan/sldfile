using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SldFile.Assembly;

/// <summary>
/// Resolves recorded Windows CAD component paths to reachable files on the host filesystem.
/// Implement this interface to provide custom path resolution strategies (e.g. database, cloud storage, vault).
/// </summary>
public interface IPathResolver
{
    /// <summary>
    /// Attempts to resolve a recorded component path to an existing file on the local host system.
    /// </summary>
    /// <param name="recordedPath">The original file path as recorded by SolidWorks.</param>
    /// <param name="parentAssemblyPath">The path of the containing parent assembly, if known.</param>
    /// <returns>The resolved local file path, or null if it cannot be found.</returns>
    string? Resolve(string recordedPath, string? parentAssemblyPath = null);
}

/// <summary>
/// Prefix substitution rule mapping Windows recorded paths to local host filesystem paths.
/// </summary>
public sealed class PathSubstitution
{
    public string WindowsPrefix { get; set; } = string.Empty;
    public string LocalPrefix { get; set; } = string.Empty;

    public PathSubstitution() { }

    public PathSubstitution(string windowsPrefix, string localPrefix)
    {
        WindowsPrefix = windowsPrefix ?? string.Empty;
        LocalPrefix = localPrefix ?? string.Empty;
    }
}

/// <summary>
/// Configuration options for resolving Windows CAD paths on arbitrary host environments.
/// </summary>
public sealed class PathResolverOptions
{
    /// <summary>Prefix substitutions to map paths (e.g. "C:\Parts\" -> "/mnt/data/parts/").</summary>
    public IList<PathSubstitution> Substitutions { get; } = new List<PathSubstitution>();

    /// <summary>Additional search directories to look for unmapped component files.</summary>
    public IList<string> SearchDirectories { get; } = new List<string>();

    /// <summary>Whether filename matching should be case-insensitive on Linux/Unix. Default true.</summary>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>Whether to search recursively in search directories. Default false.</summary>
    public bool RecursiveSearch { get; set; } = false;
}

/// <summary>
/// Default implementation of IPathResolver that resolves Windows recorded component paths
/// using prefix substitution rules, local sibling directories, and fallback folder searches.
/// </summary>
public sealed class PathResolver : IPathResolver
{
    private readonly PathResolverOptions _options;

    public PathResolver(PathResolverOptions? options = null)
    {
        _options = options ?? new PathResolverOptions();
    }

    /// <summary>
    /// Attempts to resolve a recorded component path to an existing file on the local host system.
    /// </summary>
    public string? Resolve(string recordedPath, string? parentAssemblyPath = null)
    {
        if (string.IsNullOrWhiteSpace(recordedPath))
            return null;

        // 1. If direct path already exists on local machine
        if (File.Exists(recordedPath))
            return Path.GetFullPath(recordedPath);

        string normalizedRecorded = recordedPath.Replace('\\', '/');
        string fileName = Path.GetFileName(normalizedRecorded);

        // 2. Try relative to the parent assembly's directory
        if (!string.IsNullOrEmpty(parentAssemblyPath))
        {
            string? asmDir = Path.GetDirectoryName(parentAssemblyPath);
            if (!string.IsNullOrEmpty(asmDir) && Directory.Exists(asmDir))
            {
                // Direct file in assembly dir
                string localInAsmDir = Path.Combine(asmDir, fileName);
                string? matched = MatchFileCase(localInAsmDir);
                if (matched != null) return matched;
            }
        }

        // 3. Try prefix substitutions
        foreach (var sub in _options.Substitutions)
        {
            if (string.IsNullOrEmpty(sub.WindowsPrefix)) continue;
            string normWin = sub.WindowsPrefix.Replace('\\', '/');
            if (normalizedRecorded.StartsWith(normWin, StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalizedRecorded.Substring(normWin.Length).TrimStart('/');
                string localTarget = Path.Combine(sub.LocalPrefix, relative.Replace('/', Path.DirectorySeparatorChar));
                string? matched = MatchFileCase(localTarget);
                if (matched != null) return matched;
            }
        }

        // 4. Try configured search directories
        foreach (var dir in _options.SearchDirectories)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

            string shallowTarget = Path.Combine(dir, fileName);
            string? matched = MatchFileCase(shallowTarget);
            if (matched != null) return matched;

            if (_options.RecursiveSearch)
            {
                var found = FindFileRecursive(dir, fileName);
                if (found != null) return found;
            }
        }

        return null;
    }

    private string? MatchFileCase(string filePath)
    {
        if (File.Exists(filePath))
            return Path.GetFullPath(filePath);

        if (!_options.CaseInsensitive)
            return null;

        string? dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return null;

        string expectedName = Path.GetFileName(filePath);
        var entries = Directory.GetFiles(dir);
        foreach (var entry in entries)
        {
            if (string.Equals(Path.GetFileName(entry), expectedName, StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(entry);
        }

        return null;
    }

    private string? FindFileRecursive(string rootDir, string targetFileName)
    {
        try
        {
            var files = Directory.GetFiles(rootDir, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (string.Equals(Path.GetFileName(file), targetFileName,
                    _options.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    return Path.GetFullPath(file);
                }
            }
        }
        catch
        {
            // Ignore access denied on certain subdirectories
        }

        return null;
    }
}
