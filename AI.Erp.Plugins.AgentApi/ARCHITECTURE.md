# AgentApi — fork and vendor duality

This plugin exposes the ERP managers (`RecordManager`, `EntityManager`, `EqlCommand`) as a REST
API protected by JWT, so an external AI agent can control the ERP. The plugin is written **once**
and built for **two targets**:

- **fork** — this repository (`AI.Erp` namespaces, `AI.Erp` packages);
- **vendor** — the upstream WebVella-ERP (`WebVella.Erp` namespaces, `WebVella.Erp` packages).

The fork is a rename of the vendor. The code is the same, the authors are the same, the history
is the same. Only the prefix `WebVella.Erp` became `AI.Erp` in the namespaces, the assemblies
and the package ids. The plugin only has to handle this rename.

## Why one DLL cannot serve both targets

The assembly identity is different (`AI.Erp.dll` against `WebVella.Erp.dll`), and the namespaces
are different. A plugin built against one target cannot load against the other. So the unit that
is shared is the **source**, not the binary: one source tree, two build targets, two output
assemblies (`AI.Erp.Plugins.AgentApi` and `WebVella.Erp.Plugins.AgentApi`). Each assembly is
loaded into its own ERP. This is correct. The plugin never needs to talk to both targets at the
same time.

## The mechanism: source-level `using` aliases and an `ErpFlavor` switch

The plugin source **never** writes a core namespace as text (`AI.Erp.Api`, `WebVella.Erp.Api`,
and so on). It uses only aliases: `ErpCore`, `ErpApi`, `ErpEql`. The csproj sets the alias
mapping for each target with the C# 10 global `Using` item:

| Alias | fork target | vendor target | What it points to |
|---|---|---|---|
| `ErpCore` | `AI.Erp` | `WebVella.Erp` | the `ErpPlugin` base class |
| `ErpApi`  | `AI.Erp.Api` | `WebVella.Erp.Api` | `RecordManager`, `EntityManager`, `EntityRecord`, `Entity`, `Field` |
| `ErpEql`  | `AI.Erp.Eql` | `WebVella.Erp.Eql` | `EqlCommand`, `EqlParameter` |

Build the fork target:

```bash
dotnet build AgentApi.csproj -p:ErpFlavor=fork
```

Build the vendor target:

```bash
dotnet build AgentApi.csproj -p:ErpFlavor=vendor
```

The default is `fork` when `ErpFlavor` is not set, so the plugin works in this repository
solution without any extra option.

> This is a **source `using` alias**. It is not a C# *extern/assembly alias*. An extern alias is
> used to load two versions of the same assembly in one process. It is not needed here, and it
> would be the wrong tool.

## What is the same on both targets

- The route prefix `api/v3.0/p/agent/...` and every endpoint.
- The plugin manifest (`Name = "agent"`, `Prefix = "agent"`, the version).
- The authentication: the host JWT bearer scheme. The controllers only declare `[Authorize]`.
- The authorization: the ERP per-entity permissions (`SecurityContext`). The agent works as a
  user with a role. The plugin does not add a second permission system.
- All the behavior. Only the alias mapping, the `AssemblyName` and the `PackageId` change for
  each target.

## Conditions for the duality

1. **The used API must stay a pure rename.** Every core member the plugin uses (`RecordManager`
   CRUD, `EntityManager` reads, `EqlCommand`, `EntityRecord`, `ConvertToEntityRecord`,
   `ErpPlugin`) must keep the **same signature** on the fork and on the vendor. The alias does
   **not** fix a signature change. The vendor build simply fails to compile. Keep the dependency
   surface on stable core APIs only.
2. **No type resolution from strings.** Do not use `Type.GetType("WebVella.Erp.Api.X")` or
   `Activator.CreateInstance("AI.Erp...")`. Aliases work at compile time and do nothing to
   runtime strings.
3. **No assembly-qualified names in saved JSON.** If a DTO writes its assembly name in a saved
   payload, the fork and the vendor payloads differ. `EntityRecord` is a plain dictionary, so it
   is safe. The envelopes in this plugin have no assembly names.
4. **No literal core `using`.** One `using AI.Erp.Api;` in any file breaks the vendor build. Use
   the aliases everywhere.

## Keep the aliased surface small

The fewer core namespaces the plugin uses, the smaller the risk of divergence. The plugin defines
its **own** response body (a small `AgentResponse` class) instead of the host `ResponseModel`. So
the aliased surface is only the three core namespaces above. The plugin does not use `AuthService`:
authentication is the host job, and the controller only declares `[Authorize]`.

## The agent side tool has no duality problem

`ErpTool` (the AIOrchestrator plugin that calls this API) uses only HTTP and the stable REST
contract. It does not know and does not care if the backend is the fork or the vendor. The
endpoints, the JWT and the JSON are the same. So the duality problem exists **only** in this
ERP-side plugin.

## Upstream

To contribute to WebVella, the vendor target is the canonical form. The source already uses the
aliases, and the vendor target mapping (`ErpApi` to `WebVella.Erp.Api`, and so on) is already in
this csproj. No source rewrite is needed to donate the plugin. Only the target mapping is needed,
and it already exists.
