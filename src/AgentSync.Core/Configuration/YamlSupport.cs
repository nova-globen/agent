using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSync.Core.Configuration;

/// <summary>Shared YamlDotNet configuration (snake_case keys, lenient on extras).</summary>
internal static class YamlSupport
{
    public static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
}

/// <summary>Thrown when a YAML document cannot be parsed.</summary>
public sealed class ConfigParseException : Exception
{
    public ConfigParseException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
