Refine the GUI plan for Agent Sync (`agent ui`, .NET MAUI Blazor Hybrid).

1. Read `.ai-agent/features/UI_MAUI_BLAZOR.md` and `.ai-agent/features/ROADMAP.md`
   (Milestones F–H).
2. Honor the locked decision (UI_MAUI_BLAZOR.md → "Decision (locked)"): GUI is a
   separate optional app; Windows/macOS use official MAUI Blazor Hybrid; Linux is an
   experimental OpenMaui/maui-linux spike that must not block CLI releases. Refine the
   remaining open items only: the `agent ui` launch contract and the OpenMaui Linux
   spike plan. Do not relitigate the locked decision.
3. Keep the hard rules: no repository mutation logic in Razor components; reuse
   `AgentSync.Core` services; the headless stack (CLI, git-agent, hooks, CI, dotnet
   tool packages, containers) must build/test/run/ship without MAUI/OpenMaui.
4. This is planning only — do not scaffold `AgentSync.Ui` unless asked. No AI/Claude
   trailers in commits.
