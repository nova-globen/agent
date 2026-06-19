# AgentSync.Ui.Maui (GUI skeleton)

The optional Agent Sync desktop GUI — a **separate, independent product surface** built
with **.NET MAUI Blazor Hybrid**. The CLI (`agent`, `git-agent`), Git hooks, CI, the
container images, and the `dotnet tool` packages never depend on this project or on any
GUI workload. The CLI's `agent ui` command only *launches* the built executable
(`agent-sync-ui`) by process — it has no compile-time reference to this project.

## Status

**Skeleton (Milestone G).** This project is intentionally **excluded from
`AgentSync.slnx`** so the headless `dotnet build` / `dotnet test` never require the MAUI
workload. The full screen set (Dashboard, Skills, Imports, Targets, Drift/Status, Diff,
Hooks/CI, Settings, Logs) lands in Milestone H (GUI MVP).

## Architecture rule

```text
Razor components  ->  AgentSync.Ui.Abstractions (AgentSyncApp)  ->  AgentSync.Core
```

Components read state and invoke actions through the injected `AgentSyncApp` application
service. **No repository mutation logic lives in Razor components**, and no CLI logic is
duplicated — both the CLI and the GUI call the same `AgentSync.Core` services.

## Building (requires the MAUI workload)

This project is **not** built by the default solution. On a machine with the workload:

```bash
dotnet workload install maui
dotnet build src/AgentSync.Ui.Maui -c Release -f net10.0-windows10.0.19041.0   # Windows
dotnet build src/AgentSync.Ui.Maui -c Release -f net10.0-maccatalyst           # macOS
```

Windows and macOS are the official GUI targets. A Linux GUI is an **experimental**
OpenMaui (`open-maui/maui-linux`) spike — see
[`.ai-agent/features/UI_MAUI_BLAZOR.md`](../../.ai-agent/features/UI_MAUI_BLAZOR.md) and
the OpenMaui evaluation notes in
[`.ai-agent/features/OPENMAUI_LINUX_SPIKE.md`](../../.ai-agent/features/OPENMAUI_LINUX_SPIKE.md).
It is not produced from this project and is not a supported artifact.
