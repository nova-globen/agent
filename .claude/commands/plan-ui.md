Plan/extend the Agent Sync GUI (`agent ui`) — produce a plan before implementing.

1. Read `.ai-agent/features/UI_LOCALHOST_BLAZOR.md` and `.ai-agent/features/ROADMAP.md`
   (Milestones UI-1 … UI-3).
2. Honor the decision (UI_LOCALHOST_BLAZOR.md → "Decision"):
   - The GUI is a **separate, optional localhost Blazor Web UI** (`agent-sync-ui`) built
     with **Microsoft FluentUI Blazor components**. It is **not** MAUI/OpenMaui — that
     direction was dropped.
   - The CLI/Git extension stay **headless**; hooks, CI, containers, and `dotnet tool`
     packages stay GUI-free. **Do not** make `AgentSync.Cli` reference `AgentSync.Ui.Web`
     or FluentUI.
   - `agent ui` is a **launcher/discovery command** that starts `agent-sync-ui` with
     `--repo`/`--port`/`--token`, opens the loopback URL, and fails gracefully (exit 3)
     when the GUI is absent.
   - The host binds **`127.0.0.1`** with a **random port** and a **per-launch session
     token** (exchanged into an HttpOnly cookie and stripped from the URL on first use,
     with an unauthenticated `/healthz` readiness endpoint); never bind `0.0.0.0`.
     File-writing actions use explicit submit buttons; destructive actions (delete, force
     sync, install hooks) need a second confirmation step.
   - UI operations call shared services (`AgentSync.Ui.Abstractions` → `AgentSync.Core`);
     **no repository mutation logic in Razor components**.
3. Produce a concrete implementation plan (which screens/mutations to wire, confirmation
   flow, packaging) **before writing code**. Do not relitigate the decision.
4. Keep the headless build/test green and free of UI dependencies. No AI/Claude trailers
   in commits.
