# AI.Erp.Plugins.AgentApi

This is an ERP plugin. It exposes the ERP managers (`RecordManager`, `EntityManager`,
`EqlCommand`) as a REST API protected by JWT. An external AI agent can then control the ERP.
Every call goes through the ERP managers, so the ERP hooks, validation and the permissions of
the logged-in user still apply. The agent works as a normal user. It never connects to the
database directly.

The agent side tool is **[ErpTool](https://github.com/Graphene-Lab/ErpTool)**.

## Two build targets (fork and vendor)

The same source builds for this fork (`AI.Erp`) and for the upstream `WebVella.Erp`. The build
uses source-level `using` aliases and an `ErpFlavor` switch. See **[ARCHITECTURE.md](ARCHITECTURE.md)**
for the full design, the conditions and the upstream notes.

```bash
dotnet build                       # fork (default)
dotnet build -p:ErpFlavor=vendor   # vendor (WebVella.Erp)
```

## REST endpoints

All endpoints start with `api/v3.0/p/agent`. All need `[Authorize]` (JWT bearer). All return the
same small `AgentResponse` body: `{ success, data, error, errors }`.

| Method | Route | What it does |
|---|---|---|
| `GET` | `schema` | Lists every entity and its fields (name, label, type, required). |
| `GET` | `schema/{entity}` | Lists the fields of one entity. |
| `POST` | `query` | Runs an EQL `SELECT`. Body `{ eql, parameters:[{name,value}] }`. |
| `POST` | `records/{entity}` | Creates a record from a JSON object of field values. |
| `PATCH` | `records/{entity}/{id}` | Updates a record by id. |
| `DELETE` | `records/{entity}/{id}` | Deletes a record by id. |
| `POST` | `relations` | Adds or removes a many-to-many link. Body `{ relationId, originId, targetId, remove }`. |

The JWT comes from the host endpoint `POST api/v3/en_US/auth/jwt/token`. For reads you can also
use the ERP built-in `POST api/v3/en_US/eql`.

## Host integration

The host site needs two small changes. After you reference the assembly, ASP.NET finds the
controllers automatically.

1. Reference the plugin: in `AI.Erp.Site.csproj` add
   `<ProjectReference Include="..\AI.Erp.Plugins.AgentApi\AI.Erp.Plugins.AgentApi.csproj" />`.
   On the vendor, reference the `WebVella.Erp.Plugins.AgentApi` build in the same way.
2. Register the plugin in the pipeline: in `Startup.Configure` add
   `.UseErpPlugin<AgentApiPlugin>()`.

On this fork both changes are already in `AI.Erp.Site`.

## License

Apache-2.0, the same license as the `AI.Erp` core.
