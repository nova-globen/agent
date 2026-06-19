using System.Text;
using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;

namespace AgentSync.Core.Authoring;

/// <summary>
/// Renders an <see cref="AgentConfig"/> back to a deterministic <c>agent.yaml</c>
/// document: version, then targets in canonical order (plus any extras, so editing one
/// target never drops another), then the policy block. Values go through
/// <see cref="Yaml.Scalar"/>.
/// </summary>
/// <remarks>
/// This is a round-trip of the parsed model, so hand-written comments in
/// <c>agent.yaml</c> are not preserved by an edit.
/// </remarks>
public static class ConfigYaml
{
    public static string Render(AgentConfig config)
    {
        var sb = new StringBuilder();
        sb.Append("version: ").Append(config.Version == 0 ? AgentConfig.SupportedVersion : config.Version).Append('\n');
        sb.Append('\n');
        sb.Append("targets:\n");

        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in TargetIds.Ordered)
        {
            if (config.Targets.TryGetValue(id, out var setting))
            {
                AppendTarget(sb, id, setting);
                written.Add(id);
            }
        }

        // Preserve any non-canonical targets the user has (ConfigValidator will flag them,
        // but we must not silently delete them on an unrelated edit).
        foreach (var (id, setting) in config.Targets)
        {
            if (!written.Contains(id))
            {
                AppendTarget(sb, id, setting);
            }
        }

        var p = config.Policy;
        sb.Append("policy:\n");
        sb.Append("  fail_on_missing_projection: ").Append(Bool(p.FailOnMissingProjection)).Append('\n');
        sb.Append("  fail_on_outdated_projection: ").Append(Bool(p.FailOnOutdatedProjection)).Append('\n');
        sb.Append("  fail_on_manual_edit: ").Append(Bool(p.FailOnManualEdit)).Append('\n');
        sb.Append("  allow_target_specific_overrides: ").Append(Bool(p.AllowTargetSpecificOverrides)).Append('\n');

        return sb.ToString();
    }

    private static void AppendTarget(StringBuilder sb, string id, TargetSetting setting)
    {
        sb.Append("  ").Append(id).Append(":\n");
        sb.Append("    enabled: ").Append(Bool(setting.Enabled)).Append('\n');
        if (!string.IsNullOrEmpty(setting.Path))
        {
            sb.Append("    path: ").Append(Yaml.Scalar(setting.Path)).Append('\n');
        }

        sb.Append('\n');
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
