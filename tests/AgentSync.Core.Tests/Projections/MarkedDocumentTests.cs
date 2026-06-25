using AgentSync.Core.Projections;

namespace AgentSync.Core.Tests.Projections;

public sealed class MarkedDocumentTests
{
    [Fact]
    public void Upsert_IntoEmptyDocument_CreatesSection()
    {
        var doc = MarkedDocument.Parse(string.Empty);

        var result = doc.Upsert("code-review", "agents_md", "Generated body.");

        Assert.Equal(ProjectionChange.Created, result.Change);
        var rendered = doc.Render();
        Assert.Contains("agent-sync:start id=code-review target=agents_md", rendered);
        Assert.Contains("Generated body.", rendered);
        Assert.Contains(Markers.End, rendered);
    }

    [Fact]
    public void Parse_RoundTripsManagedAndLiteralContent()
    {
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("s1", "agents_md", "Body one");
        var rendered = doc.Render();

        var reparsed = MarkedDocument.Parse(rendered);
        var section = reparsed.Find("s1", "agents_md");

        Assert.NotNull(section);
        Assert.Equal("Body one", section!.Body);
        Assert.False(section.IsManuallyEdited);
        Assert.Equal(rendered, reparsed.Render());
    }

    [Fact]
    public void Upsert_PreservesSurroundingUserContent()
    {
        const string original = "# My File\n\nUser intro paragraph.\n";
        var doc = MarkedDocument.Parse(original);

        doc.Upsert("s1", "claude_md", "Managed text");

        var rendered = doc.Render();
        Assert.StartsWith("# My File\n\nUser intro paragraph.\n", rendered);
        Assert.Contains("Managed text", rendered);
    }

    [Fact]
    public void Upsert_SameContent_ReturnsUnchanged()
    {
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("s1", "agents_md", "Stable body");
        var reparsed = MarkedDocument.Parse(doc.Render());

        var result = reparsed.Upsert("s1", "agents_md", "Stable body");

        Assert.Equal(ProjectionChange.Unchanged, result.Change);
    }

    [Fact]
    public void Upsert_NewContent_UpdatesSection()
    {
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("s1", "agents_md", "Old body");
        var reparsed = MarkedDocument.Parse(doc.Render());

        var result = reparsed.Upsert("s1", "agents_md", "New body");

        Assert.Equal(ProjectionChange.Updated, result.Change);
        Assert.Contains("New body", reparsed.Render());
        Assert.DoesNotContain("Old body", reparsed.Render());
    }

    [Fact]
    public void Upsert_ManuallyEditedSection_IsSkippedWithoutForce()
    {
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("s1", "agents_md", "Generated body");
        // Simulate a hand edit: keep the marker+hash but change the body text.
        var tampered = doc.Render().Replace("Generated body", "Human edited this");

        var reparsed = MarkedDocument.Parse(tampered);
        Assert.True(reparsed.Find("s1", "agents_md")!.IsManuallyEdited);

        var result = reparsed.Upsert("s1", "agents_md", "Newly generated", force: false);

        Assert.Equal(ProjectionChange.SkippedManualEdit, result.Change);
        Assert.True(result.ManualEditDetected);
        Assert.Contains("Human edited this", reparsed.Render());
        Assert.DoesNotContain("Newly generated", reparsed.Render());
    }

    [Fact]
    public void Upsert_ManuallyEditedSection_IsReplacedWithForce()
    {
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("s1", "agents_md", "Generated body");
        var tampered = doc.Render().Replace("Generated body", "Human edited this");
        var reparsed = MarkedDocument.Parse(tampered);

        var result = reparsed.Upsert("s1", "agents_md", "Newly generated", force: true);

        Assert.Equal(ProjectionChange.Updated, result.Change);
        Assert.True(result.ManualEditDetected);
        Assert.Contains("Newly generated", reparsed.Render());
    }

    [Fact]
    public void Parse_HandlesMultipleSectionsInOneFile()
    {
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("s1", "agents_md", "First");
        doc.Upsert("s2", "agents_md", "Second");

        var reparsed = MarkedDocument.Parse(doc.Render());

        Assert.Equal(2, reparsed.Sections.Count());
        Assert.Equal("First", reparsed.Find("s1", "agents_md")!.Body);
        Assert.Equal("Second", reparsed.Find("s2", "agents_md")!.Body);
    }

    [Fact]
    public void Upsert_BodyMatchesCanonical_StaleMarkerHashReconciled()
    {
        // Simulate a coordinated migration: both canonical and generated section were edited
        // (e.g. via a repo-wide find/replace) but the marker's declared sha256 was not updated.
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("s1", "agents_md", "Old body");
        // The rendered file has the old hash in the marker.
        var rendered = doc.Render();

        // Re-parse and simulate: body was changed to match new canonical, but marker hash is stale.
        // We tamper by replacing the body text while leaving the sha256 alone (old hash).
        var oldHash = ContentHasher.Hash("Old body");
        var tampered = rendered.Replace("Old body", "New body");  // body changed, marker hash stale
        var reparsed = MarkedDocument.Parse(tampered);
        var section = reparsed.Find("s1", "agents_md")!;

        // The section is "manually edited" (body differs from declared hash).
        Assert.True(section.IsManuallyEdited);

        // Upserting with "New body" (which now matches the on-disk body) should reconcile
        // the stale hash WITHOUT requiring --force.
        var result = reparsed.Upsert("s1", "agents_md", "New body", force: false);

        Assert.Equal(ProjectionChange.Updated, result.Change);
        Assert.False(result.ManualEditDetected);

        // The re-rendered document has the new hash in the marker.
        var rerendered = reparsed.Render();
        Assert.DoesNotContain(oldHash, rerendered);
        Assert.Contains(ContentHasher.Hash("New body"), rerendered);
        Assert.Contains("New body", rerendered);
    }

    [Fact]
    public void Upsert_BodyMatchesCanonical_StaleMarkerHash_DoesNotRequireForce()
    {
        // Stale declared hash but body is already correct → plain sync reconciles it; no --force.
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("s1", "claude_md", "The body");
        // Swap the real hash for a fake-but-syntactically-valid hex string.
        const string staleHash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var rendered = doc.Render().Replace(ContentHasher.Hash("The body"), staleHash);
        var reparsed = MarkedDocument.Parse(rendered);

        Assert.NotNull(reparsed.Find("s1", "claude_md"));
        Assert.True(reparsed.Find("s1", "claude_md")!.IsManuallyEdited);
        var result = reparsed.Upsert("s1", "claude_md", "The body");

        // Reconciled without --force; marker hash is updated.
        Assert.NotEqual(ProjectionChange.SkippedManualEdit, result.Change);
        Assert.False(result.ManualEditDetected);
    }

    [Fact]
    public void BodyContainingLiteralEndMarker_RoundTripsWithoutDrift()
    {
        // A skill that documents the marker format embeds a literal agent-sync:end comment.
        const string body =
            "## When to use\n\nClose a section with `<!-- agent-sync:end -->` and open it with\n" +
            "`<!-- agent-sync:start id=x target=y hash=sha256:abc -->`.";

        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("docs", "agents_md", body);
        var rendered = doc.Render();

        // The inner end marker must be escaped so it cannot close the section early.
        Assert.Contains("&lt;!-- agent-sync:end -->", rendered);
        Assert.Single(Markers.EndMarker().Matches(rendered)); // only the section's own end

        var reparsed = MarkedDocument.Parse(rendered);
        var section = reparsed.Find("docs", "agents_md");

        Assert.NotNull(section);
        Assert.Single(reparsed.Sections);
        Assert.Equal(body, section!.Body);          // body is restored verbatim
        Assert.False(section.IsManuallyEdited);      // and not seen as hand-edited
        Assert.Equal(rendered, reparsed.Render());   // render is stable
    }

    [Fact]
    public void TwoSections_OneContainingMarkers_BothRoundTrip()
    {
        var doc = MarkedDocument.Parse(string.Empty);
        doc.Upsert("plain", "agents_md", "Just text.");
        doc.Upsert("docs", "agents_md", "Ends with <!-- agent-sync:end --> inline.");

        var reparsed = MarkedDocument.Parse(doc.Render());

        Assert.Equal(2, reparsed.Sections.Count());
        Assert.Equal("Just text.", reparsed.Find("plain", "agents_md")!.Body);
        Assert.Equal("Ends with <!-- agent-sync:end --> inline.", reparsed.Find("docs", "agents_md")!.Body);
        Assert.False(reparsed.Find("docs", "agents_md")!.IsManuallyEdited);
    }

    [Fact]
    public void Parse_UnterminatedSection_TreatedAsLiteral()
    {
        const string content = "<!-- agent-sync:start id=s1 target=agents_md hash=sha256:abc -->\nno end here";

        var doc = MarkedDocument.Parse(content);

        Assert.Empty(doc.Sections);
        Assert.Equal(content, doc.Render());
    }
}
