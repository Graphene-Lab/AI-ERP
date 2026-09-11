# AI.Erp.Plugins.AgentApi

An ERP plugin that exposes the platform's own managers — `RecordManager`, `EntityManager`,
`EqlCommand` — over a **JWT-secured REST API**, so an external AI agent can drive the ERP
with full control. Every call runs through the ERP's managers, so hooks, validation and the
authenticated user's per-entity permissions are enforced: the agent acts as a user, never as
a raw database connection.

The companion agent tool is **[ErpTool](https://github.com/Graphene-Lab/ErpTool)**.

## Dual-target (fork / vendor)

The same source builds for this fork (`AI.Erp`) **and** for upstream `WebVella.Erp` via
source-level `using` aliases + an `ErpFlavor` build switch. See **[ARCHITECTURE.md](ARCHITECTURE.md)**
for the full design, the prerequisites, and the upstreaming notes.

```bash
dotnet build                                  # fork (default)
dotnet build -p:ErpFlavor=vendor              # vendor (WebVella.Erp)
```

## REST endpoints

All under `api/v3.0/p/agent`, all `[Authorize]` (JWT bearer), all returning a minimal
`AgentResponse` envelope `{ success, data, error, errors }`.

| Method | Route | Purpose |
|---|---|---|
| `GET` | `schema` | Every entity with its fields (name, label, type, required). |
| `GET` | `schema/{entity}` | One entity's fields by name. |
| `POST` | `query` | Run an EQL `SELECT`. Body `{ eql, parameters:[{name,value}] }`. |
| `POST` | `records/{entity}` | Create a record from a field-value JSON object. |
| `PATCH` | `records/{entity}/{id}` | Update a record by id. |
| `DELETE` | `records/{entity}/{id}` | Delete a record by id. |
| `POST` | `relations` | Add/remove a many-to-many link. Body `{ relationId, originId, targetId, remove }`. |

Authentication uses the host's JWT endpoint (`POST api/v3/en_US/auth/jwt/token`). Reads can
also use the ERP's built-in `POST api/v3/en_US/eql`.

## Host integration

Two one-liners in the host site (the plugin's controllers are auto-discovered once the
assembly is referenced):

1. Reference the plugin: `AI.Erp.Site.csproj` →
   `<ProjectReference Include="..\AI.Erp.Plugins.AgentApi\AI.Erp.Plugins.AgentApi.csproj" />`
   (on the vendor, reference the `WebVella.Erp.Plugins.AgentApi` build the same way).
2. Register the plugin in the pipeline: `Startup.Configure` →
   `.UseErpPlugin<AgentApiPlugin>()`.

On this fork both are already applied in `AI.Erp.Site`.

## License

Apache-2.0 — matching the `AI.Erp` core it plugs into.
