using System;
using System.Collections.Generic;
using SldFile.Ole;

namespace SldFile.Ole;

/// <summary>
/// Represents the configuration-specific custom properties.
/// Usually found in streams like "Config-0-Properties".
/// </summary>
internal class ConfigProperties
{
    public string ConfigurationName { get; }

    public Dictionary<string, CustomProperty> CustomProperties { get; } = new Dictionary<string, CustomProperty>();

    internal byte[]? RawData;

    public ConfigProperties(string configName)
    {
        ConfigurationName = configName;
    }
}
