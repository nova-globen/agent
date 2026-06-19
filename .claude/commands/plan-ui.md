Plan the Agent Sync GUI (`agent ui`) — do not implement yet; produce a plan first.

1. Read `.ai-agent/features/UI_MAUI_BLAZOR.md` and `.ai-agent/features/ROADMAP.md`
   (Milestones F/F2/G/H).
2. Honor the locked decision (UI_MAUI_BLAZOR.md → "Decision"):
   - CLI and GUI are **separate**; the GUI is **optional**.
   - **Do not add MAUI/OpenMaui references to `AgentSync.Cli`** (or `AgentSync.GitAgent`,
     hooks, CI, containers, `dotnet tool` packages).
   - `agent ui` is a **launcher/discovery command** that starts an external
     `agent-sync-ui` executable and fails gracefully when it is absent.
   - GUI operations call shared services (`AgentSync.Core` / `AgentSync.Ui.Abstractions`);
     **no repository mutation logic in Razor components**.
   - Official GUI = MAUI Blazor Hybrid for **Windows/macOS**.
   - **Treat OpenMaui Linux support as a spike, not committed product behavior.**
3. Produce a concrete implementation plan (project split, launch contract, build
   isolation) **before writing any code**. Do not relitigate the locked decision.
4. Planning only — do not scaffold the UI projects unless explicitly asked. No
   AI/Claude trailers in commits.
