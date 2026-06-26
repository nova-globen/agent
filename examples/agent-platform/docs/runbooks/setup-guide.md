# Setup Guide

How to bootstrap this kit, wire up the git hooks, and run the first autopilot session.

## Prerequisites

- .NET 10 SDK (verify: `dotnet --version` shows `10.x.x`)
- Git
- Git Bash (if on Windows without WSL)
- Claude Code CLI (`claude` in PATH) for autopilot

## Step 1: Clone and restore tools

```bash
git clone <your-repo-url>
cd <your-repo>

# Install AgentSync (local dotnet tool)
dotnet tool restore
```

## Step 2: Wire git hooks

Run from Git Bash (or WSL/Linux):

```bash
bash scripts/setup-git-hooks.sh
```

This sets `core.hooksPath=.githooks`, makes hooks executable, and runs `dotnet agent sync` to generate
the AgentSync projections (AGENTS.md, CLAUDE.md, .claude/skills/, .gemini/GEMINI.md).

## Step 3: Verify AgentSync projections

```bash
dotnet agent status
```

Should report no drift. If it reports drift or missing projections, run `dotnet agent sync` first.

## Step 4: Verify the build

```bash
dotnet build Platform.slnx -v q -clp:ErrorsOnly -nologo
```

Should output `Build succeeded. 0 Warning(s) 0 Error(s)`.

## Step 5: Run the first autopilot session

```bash
dotnet agent autopilot claude
```

This picks up the starter handoff prompt at `.agent/prompts/autopilot/prompt-20260626-1200_hello-platform-increment-01.txt`,
implements the `Greeter` class and unit test, verifies the build and tests, commits, and writes the
next handoff prompt. Watch the TUI for progress.

When it completes, verify:
- `src/Platform.Hello/Greeter.cs` exists.
- `tests/Platform.Hello.Tests/GreeterTests.cs` exists.
- `git log --oneline -3` shows a `feat(hello-platform):` commit.
- A new `prompt-*.txt` file appears in `.agent/prompts/autopilot/`.

## Adopting for Your Project

After the worked example completes successfully:

1. **Update project-brief.md** — fill in your product description and tech stack.
2. **Update architecture-principles.md** — replace the placeholder principles with yours.
3. **Replace the worked example** — remove `src/Platform.Hello/` and `tests/Platform.Hello.Tests/`;
   add your real projects to `Platform.slnx`; update `docs/features/` and `docs/plans/`.
4. **Run `dotnet agent sync`** after any changes to `.agent/skills/` or `.agent/agents/`.
5. **Define your first real feature** and run autopilot.

## Note for kit developers

This kit lives in `examples/agent-platform/` within the `agent-sync-ai-agent-kit` repo. AgentSync walks
up to the Git root, so `dotnet agent sync` run from the parent repo targets the parent's `.agent/`, not
this kit's. To test this kit's own `agent sync` / `agent status` cycle, copy it to a separate directory
and run `git init` there first — that is the intended deployment model.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `dotnet: command not found` | Install .NET 10 SDK |
| `dotnet agent: command not found` | Run `dotnet tool restore` |
| `agent status` reports drift | Run `dotnet agent sync` |
| Hooks not firing | Run `bash scripts/setup-git-hooks.sh` |
| Build fails on Windows native | Use Git Bash or WSL; Bash scripts require POSIX sh |
