using System.Text;

namespace AgentSync.Core.Projections;

public enum ProjectionChange
{
    /// <summary>A new managed section was added.</summary>
    Created,

    /// <summary>An existing managed section was rewritten.</summary>
    Updated,

    /// <summary>The section already matched the desired content; nothing changed.</summary>
    Unchanged,

    /// <summary>The section was edited by hand and was left untouched (no force).</summary>
    SkippedManualEdit,
}

public sealed record UpsertResult(ProjectionChange Change, string Hash, bool ManualEditDetected)
{
    public bool Wrote => Change is ProjectionChange.Created or ProjectionChange.Updated;
}

/// <summary>A single generated section inside a document.</summary>
public sealed class ManagedSection
{
    public required string SkillId { get; set; }

    public required string TargetId { get; set; }

    public required string DeclaredHash { get; set; }

    public required string Body { get; set; }

    /// <summary>True if the on-disk body no longer matches its declared hash.</summary>
    public bool IsManuallyEdited => !ContentHasher.Matches(Body, DeclaredHash);
}

/// <summary>
/// A document split into literal (user-authored) text and managed generated sections.
/// Supports safe replacement: a hand-edited section is never overwritten unless forced.
/// </summary>
public sealed class MarkedDocument
{
    private sealed class Node
    {
        public string? Literal { get; init; }
        public ManagedSection? Section { get; init; }
    }

    private readonly List<Node> _nodes = new();

    private MarkedDocument()
    {
    }

    public IEnumerable<ManagedSection> Sections =>
        _nodes.Where(n => n.Section is not null).Select(n => n.Section!);

    public ManagedSection? Find(string skillId, string targetId) =>
        Sections.FirstOrDefault(s =>
            s.SkillId == skillId && s.TargetId == targetId);

    public static MarkedDocument Parse(string content)
    {
        var doc = new MarkedDocument();
        var pos = 0;

        while (pos < content.Length)
        {
            var start = Markers.StartMarker().Match(content, pos);
            if (!start.Success)
            {
                doc.AddLiteral(content[pos..]);
                break;
            }

            var end = Markers.EndMarker().Match(content, start.Index + start.Length);
            if (!end.Success)
            {
                // Unterminated section: keep everything from here as literal text.
                doc.AddLiteral(content[pos..]);
                break;
            }

            if (start.Index > pos)
            {
                doc.AddLiteral(content[pos..start.Index]);
            }

            var bodyStart = start.Index + start.Length;
            var rawBody = content[bodyStart..end.Index];
            doc._nodes.Add(new Node
            {
                Section = new ManagedSection
                {
                    SkillId = start.Groups["id"].Value,
                    TargetId = start.Groups["target"].Value,
                    DeclaredHash = start.Groups["hash"].Value,
                    Body = Markers.UnescapeBody(StripSurroundingNewlines(rawBody)),
                },
            });

            pos = end.Index + end.Length;
        }

        return doc;
    }

    public UpsertResult Upsert(string skillId, string targetId, string newBody, bool force = false)
    {
        var newHash = ContentHasher.Hash(newBody);
        var existing = Find(skillId, targetId);

        if (existing is null)
        {
            AppendSection(new ManagedSection
            {
                SkillId = skillId,
                TargetId = targetId,
                DeclaredHash = newHash,
                Body = newBody,
            });
            return new UpsertResult(ProjectionChange.Created, newHash, ManualEditDetected: false);
        }

        var manuallyEdited = existing.IsManuallyEdited;
        var currentHash = ContentHasher.Hash(existing.Body);

        if (currentHash == newHash && !manuallyEdited)
        {
            return new UpsertResult(ProjectionChange.Unchanged, newHash, ManualEditDetected: false);
        }

        if (manuallyEdited && !force)
        {
            return new UpsertResult(ProjectionChange.SkippedManualEdit, existing.DeclaredHash, ManualEditDetected: true);
        }

        existing.Body = newBody;
        existing.DeclaredHash = newHash;
        return new UpsertResult(ProjectionChange.Updated, newHash, ManualEditDetected: manuallyEdited);
    }

    public string Render()
    {
        var sb = new StringBuilder();
        foreach (var node in _nodes)
        {
            if (node.Literal is not null)
            {
                sb.Append(node.Literal);
            }
            else if (node.Section is { } s)
            {
                sb.Append(Markers.RenderStart(s.SkillId, s.TargetId, s.DeclaredHash));
                sb.Append('\n');
                sb.Append(Markers.EscapeBody(s.Body));
                sb.Append('\n');
                sb.Append(Markers.End);
            }
        }

        return sb.ToString();
    }

    private void AddLiteral(string text)
    {
        if (text.Length > 0)
        {
            _nodes.Add(new Node { Literal = text });
        }
    }

    private void AppendSection(ManagedSection section)
    {
        var current = Render();
        string separator;
        if (current.Length == 0)
        {
            separator = string.Empty;
        }
        else if (current.EndsWith("\n\n", StringComparison.Ordinal))
        {
            separator = string.Empty;
        }
        else if (current.EndsWith('\n'))
        {
            separator = "\n";
        }
        else
        {
            separator = "\n\n";
        }

        if (separator.Length > 0)
        {
            _nodes.Add(new Node { Literal = separator });
        }

        _nodes.Add(new Node { Section = section });
    }

    private static string StripSurroundingNewlines(string raw)
    {
        var body = raw;
        if (body.StartsWith("\r\n", StringComparison.Ordinal))
        {
            body = body[2..];
        }
        else if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        if (body.EndsWith("\r\n", StringComparison.Ordinal))
        {
            body = body[..^2];
        }
        else if (body.EndsWith('\n'))
        {
            body = body[..^1];
        }

        return body;
    }
}
