# AgentApi — fork / vendor duality

This plugin exposes the ERP's in-process managers (`RecordManager`, `EntityManager`,
`EqlCommand`) over a JWT-secured REST surface so an external AI agent can drive the ERP
with full control. It is written **once** and built for **two targets**:

- **fork** — this repository (`AI.Erp.*` namespaces, `AI.Erp` packages)
- **vendor** — upstream WebVella-ERP (`WebVella.Erp.*` namespaces, `WebVella.Erp` packages)

The fork is a mechanical rename of the vendor: same code, same authors, same repo lineage,
only the `WebVella.Erp` → `AI.Erp` namespace / assembly / package-id prefix changed. That
rename is the *only* difference the plugin has to absorb.

## Why one DLL cannot serve both

Assembly identity differs (`AI.Erp.dll` vs `WebVella.Erp.dll`) and so do the namespaces.
A plugin compiled against one cannot load against the other. The unit of reuse is therefore
the **source**, not the binary: one source tree, two build targets, two output assemblies
(`AI.Erp.Plugins.AgentApi` and `WebVella.Erp.Plugins.AgentApi`), each loaded into its own
ERP. This is expected and correct — the plugin never needs to talk to both cores at once.

## The mechanism: source-level global `using` aliases + a `ErpFlavor` build switch

The plugin source **never** writes a core namespace literally (`AI.Erp.Api`, `WebVella.Erp.Api`,
…). It refers to the core only through aliases — `ErpCore`, `ErpApi`, `ErpEql` — whose
mapping is injected per-target by the csproj via the C# 10 global `Using` item:

| Alias | fork target | vendor target | What it carries |
|---|---|---|---|
| `ErpCore` | `AI.Erp` | `WebVella.Erp` | `ErpPlugin` base class |
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

The default (`ErpFlavor` unset) is `fork`, so the plugin drops into this repo's solution
with no extra flag.

> This is a **source `using` alias**, not a C# *extern/assembly alias*. An extern alias is
> for loading two versions of the same assembly side-by-side in one process — not needed
> here and would be the wrong tool.

## What stays identical across both targets

- Route prefix `api/v3.0/p/agent/...` and every endpoint contract.
- The plugin manifest (`Name = "agent"`, `Prefix = "agent"`, version).
- Authentication: the host's JWT bearer scheme; controllers are plain `[Authorize]`.
- Authorization: the ERP's own per-entity permissions (`SecurityContext`) — the agent acts
  as a user whose role grants entity access. The plugin adds no parallel permission model.
- All behavior. Only the alias mapping, `AssemblyName`, and `PackageId` flip per target.

## Prerequisites for the duality to hold

1. **Pure rename of the used surface.** Every core member the plugin touches
   (`RecordManager` CRUD, `EntityManager` reads, `EqlCommand`, `EntityRecord`,
   `ConvertToEntityRecord`, `ErpPlugin`) must keep an **identical signature** between fork
   and vendor. The alias does **not** paper over a signature change — the vendor build simply
   fails to compile. Keep the dependency surface on stable, long-lived core APIs only.
2. **No string-based type resolution.** Never `Type.GetType("WebVella.Erp.Api.X")` or
   `Activator.CreateInstance("AI.Erp…")` — aliases are compile-time and do nothing to
   runtime strings.
3. **No assembly-qualified names in persisted JSON.** If a DTO embeds its assembly name in
   serialized payloads, fork/vendor payloads diverge. `EntityRecord` is a plain dictionary
   and is safe; custom envelopes here are kept assembly-name-free.
4. **Discipline: no literal core `using`.** A stray `using AI.Erp.Api;` in any file breaks
   the vendor build. Route everything through the aliases.

## Hardening: minimize the aliased surface

The fewer core namespaces the plugin touches, the smaller the divergence risk. The plugin
defines its **own** response envelope (a small `AgentResponse` POCO) instead of reusing the
host's `ResponseModel`, so the aliased surface is reduced to the three core namespaces above.
`AuthService` is not referenced: authentication is the host's job; the controller only
declares `[Authorize]`.

## The agent-side tool is already target-agnostic

`ErpTool` (the AIOrchestrator plugin that calls this API) is pure HTTP against the stable
REST contract. It does not care whether the backend ERP is the fork or the vendor — same
endpoints, same JWT, same JSON. The duality concern lives **only** in this ERP-side plugin.

## Upstreaming

To contribute back to WebVella, the vendor target is the canonical form: the source uses
aliases, and the vendor adds its own `ErpFlavor=vendor` mapping (`ErpApi → WebVella.Erp.Api`,
…). No source rewrite is required to donate — only the target mapping, which already exists
in this csproj.
