Evaluate OpenMaui (`open-maui/maui-linux`) as an experimental Linux GUI host for Agent
Sync. This is **Milestone F2** — a spike, not committed product behavior.

1. Read `.ai-agent/features/UI_MAUI_BLAZOR.md` (especially "OpenMaui Linux evaluation")
   and `.ai-agent/features/ROADMAP.md` (Milestone F2). Repo under evaluation:
   `https://github.com/open-maui/maui-linux`.
2. Create or plan a **minimal** OpenMaui Linux spike (`src/AgentSync.Ui.Linux.OpenMaui/`),
   kept out of the normal headless build/test path.
3. Verify whether a Linux GUI can:
   - build and run on Ubuntu/Debian and one RPM-based distro;
   - host the planned Blazor (or equivalent Razor) UI;
   - call `AgentSync.Core` services;
   - open/select a repository folder and show status / run a read-only validation.
4. Check X11/Wayland behavior, packaging feasibility (e.g. AppImage), and maintenance
   risk.
5. **Do not** change production CLI behavior. **Do not** add OpenMaui to the normal
   headless build/test path, the release workflow, or any CLI/`dotnet tool` dependency.
   Do not claim Linux GUI support without proven build + runtime.
6. Report a clear recommendation: **accept** (experimental Linux GUI path), **defer**,
   or **reject** (Linux stays CLI-first; note alternatives — local Blazor web UI,
   Avalonia, Uno Platform, Photino). No AI/Claude trailers in commits.
