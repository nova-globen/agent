# Solution Structure

<!-- Document your solution layout here. Replace the worked-example content below with your project. -->

## Current Layout

```
Platform.slnx
├── src/
│   └── Platform.Hello/          ← worked example: simple class library
│       Platform.Hello.csproj
└── tests/
    └── Platform.Hello.Tests/    ← worked example: unit tests
        Platform.Hello.Tests.csproj
```

## Project Conventions

<!-- Describe your module/layer conventions here. Examples: -->

- **`src/<Module>/`** — production code. Each module is self-contained.
- **`tests/<Module>.Tests/`** — unit tests, mirroring the source project structure.
- **`tests/<Module>.IntegrationTests/`** — integration tests (Docker/infra-dependent).

## Shared Foundations

<!-- List shared/cross-cutting projects here (SharedKernel, BuildingBlocks, etc.) -->

## Dependency Rules

See `.agent/context/architecture-principles.md` for the governing dependency direction and boundary rules.

## Adding a New Module

<!-- Describe the steps to add a new module/project. Link to a runbook if one exists. -->

1. Create the project folder under `src/` and `tests/`.
2. Add project references to `Platform.slnx`.
3. Add a feature spec to `docs/features/` and a plan to `docs/plans/`.
4. Run `dotnet build Platform.slnx` to confirm the solution builds.
