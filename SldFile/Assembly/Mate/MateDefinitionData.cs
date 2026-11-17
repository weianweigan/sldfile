using System;
using System.Collections.Generic;

namespace SldFile.Assembly.Mate;

// TODO: Remove

/// <summary>
/// Base class for public assembly mate definitions. Pure domain object with standard SI units and zero MFC dependencies.
/// </summary>
public abstract class AssemblyMateDefinitionData
{
    public string DisplayName { get; set; } = string.Empty;
    public abstract AssemblyMateType MateType { get; }
    public AssemblyMateAlignment Alignment { get; set; } = AssemblyMateAlignment.Aligned;
    public List<string> EntitiesToMate { get; } = new();
}
