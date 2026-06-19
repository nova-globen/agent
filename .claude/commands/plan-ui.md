Refine the GUI plan for Agent Sync (`agent ui`, .NET MAUI Blazor Hybrid).

1. Read `.ai-agent/features/UI_MAUI_BLAZOR.md` and `.ai-agent/features/ROADMAP.md`
   (Milestones F–H).
2. Resolve the open architecture decisions: separate publishable app vs bundled into
   `agent`; the `agent ui` launch contract; the Windows/macOS/Linux support matrix and
   whether Linux GUI is in scope for alpha. Record the decisions in the doc.
3. Keep the hard rules: no repository mutation logic in Razor components; reuse
   `AgentSync.Core` services; the headless CLI must build/test/run without the MAUI
   workload and must not depend on MAUI.
4. This is planning only — do not scaffold `AgentSync.Ui` unless asked. No AI/Claude
   trailers in commits.
