# Promotion copy — Agent Sync (v0.2.0)

Reusable copy for announcing Agent Sync. Adjust tone per channel.
Repo: https://github.com/nova-globen/agent

## Tagline

> Write your AI-agent instructions once. Keep `AGENTS.md`, `CLAUDE.md`, Cursor, Copilot,
> Gemini, and skill folders in sync — and let Git catch the drift.

## Feature summary (5 bullets)

- **One canonical source** — author a skill once under `.agent/`; project it to
  `AGENTS.md`, `CLAUDE.md`, Cursor rules, Copilot instructions, Gemini instructions, and
  OpenAI/Claude skill folders.
- **Drift detection** — `agent status` flags missing, outdated, or hand-edited
  generated sections; `--fail-on-drift --ci` exits non-zero for pipelines.
- **Safe by design** — generated content lives between stable markers and never
  clobbers your hand-written prose; manual edits are detected, not overwritten.
- **Git-native** — `agent` and `git agent`, plus pre-commit/pre-push hooks that block
  commits when instructions drift.
- **No runtime needed** — self-contained `agent` / `git-agent` binaries for Linux,
  macOS, and Windows.

## Short post (LinkedIn / Twitter / X)

> Tired of `AGENTS.md`, `CLAUDE.md`, Cursor rules, and Copilot instructions slowly
> drifting apart?
>
> Agent Sync lets you write AI-agent instructions once and mirror them into every
> format — then uses Git hooks + CI to catch drift before it ships.
>
> Open source (AGPL-3.0). Try it: https://github.com/nova-globen/agent

## Longer post (GitHub Discussions / Reddit / Hacker News)

> **Agent Sync: keep your AI-agent instruction files in sync, Git-native**
>
> If you use more than one AI coding agent in a repo, you've probably got the same
> guidance copy-pasted across `AGENTS.md`, `CLAUDE.md`, `.cursor/rules/*`,
> `.github/copilot-instructions.md`, `.gemini/GEMINI.md`, and per-agent skill folders.
> They drift. Someone edits one, forgets the rest, and the agents start disagreeing.
>
> Agent Sync treats one `.agent/` directory as the source of truth. You write a skill
> once (`skill.yaml` + `SKILL.md`) and `agent sync` projects it into every configured
> target. Generated content goes between stable `agent-sync` markers, so it never
> overwrites your hand-written prose, and hand-edits are detected rather than clobbered.
>
> `agent status --fail-on-drift --ci` makes drift a CI failure, and the bundled Git
> hooks block commits/pushes when generated instructions are out of date. It ships as
> `agent` plus a `git agent` extension, with self-contained binaries for Linux, macOS,
> and Windows.
>
> The core flow is solid and has been used end to end on real repositories. Feedback
> is welcome: which agents you use, which targets you enabled, and what broke or felt
> confusing.
>
> Repo + quick demo: https://github.com/nova-globen/agent
> License: AGPL-3.0-or-later.

## Demo script (copy/paste)

```bash
mkdir agent-sync-demo && cd agent-sync-demo
git init

agent init        # scaffold .agent/ with a default code-review skill
agent sync        # project it into AGENTS.md, CLAUDE.md, Cursor, Copilot, Gemini, skill folders
agent status --fail-on-drift --ci
agent install-hooks

# Hand-edit a generated section in AGENTS.md (between the agent-sync markers), then:
agent status --fail-on-drift --ci          # reports a manually edited projection
git commit --allow-empty -m "drift demo"   # blocked by the pre-commit hook

agent sync --force                         # regenerate from the canonical source
agent status --fail-on-drift --ci          # green again
```

## Feedback wanted

This is an alpha — real-world usage is exactly what it needs. Especially useful:

- Which AI agents/tools do you use, and which generated targets did you enable?
- Repository type, language, and framework.
- Did `sync` / `status` / hooks / CI work as expected on your OS?
- What broke, surprised you, or felt confusing?
- Adapter requests for tools we don't cover yet.

Please open an issue using the templates (bug report, feature request, adapter request,
or real-world repo feedback): https://github.com/nova-globen/agent/issues/new/choose
