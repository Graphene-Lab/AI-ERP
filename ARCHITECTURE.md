# Architecture

This page explains how AI-ERP is put together, from the core to the AI-agent tool.

## What it is

AI-ERP is a metadata-driven ERP. The data model (entities and fields) is stored as data, not
as fixed code. The same engine reads that model and builds the API, the storage and the
screens on top of it. This fork is a rename of the open-source WebVella ERP (`WebVella.Erp`
became `AI.Erp`), plus a plugin that lets an AI agent drive the ERP.

## The solution, top to bottom

| Project | Role |
|---|---|
| `AI.Erp` | The core engine: entities, records, the EQL query language, security, the plugin base. |
| `AI.Erp.Web` | The web layer: pages, tag helpers, the host that runs the app. |
| `AI.Erp.Site` | The runnable web app. Holds `config.json` and `bootstrap.json`. This is what you start. |
| `AI.Erp.Plugins.AgentApi` | The plugin that exposes the ERP to an AI agent over a JWT REST API. |
| `AI.Erp.Plugins.*` (others) | Other plugins (Mail, SDK, CRM, and so on), inherited from the vendor. |

Data lives in **PostgreSQL**. The core talks to it through its own managers
(`RecordManager`, `EntityManager`) and the EQL query language.

## The fork and the vendor

The fork is a rename of the upstream WebVella ERP. The code, authors and history are the same;
only the `WebVella.Erp` prefix became `AI.Erp`. The AgentApi plugin is written once and builds
for both targets through source `using` aliases and an `ErpFlavor` switch. See
[AgentApi architecture](AI.Erp.Plugins.AgentApi/ARCHITECTURE.md) for the details.

## How an AI agent drives the ERP

```
You (plain language)
   ↓
AgentBridge  →  AIOrchestrator (the agent loop)
   ↓  picks a tool method
ErpTool  →  HTTPS + JWT  →  AgentApi  →  AI.Erp managers  →  PostgreSQL
```

- **ErpTool** is the agent-side tool. It only speaks HTTP and JSON. It does not know if the
  backend is the fork or the vendor.
- **AgentApi** is the ERP-side plugin. Every call runs through the ERP's own managers, so the
  hooks, the validation and the logged-in user's permissions all apply. The agent acts as a
  normal user, never as a raw database connection.

The AgentApi has three kinds of endpoints:

1. **Discovery and CRUD** — read the schema, create/update/delete records, run EQL queries.
2. **Composed operations** — one call does many steps: place an order, deliver it, invoice it,
  collect a payment; the purchase-to-pay cycle; stock moves; and the manager reports.
3. **Setup** — check the install state (`setup-status`) and re-apply the setup (`reprovision`).

## Headless first-run setup

There is no interactive installer. On first start the AgentApi plugin reads `bootstrap.json`
and creates the business schema and the seed data by itself, inside a system security scope.
The run is idempotent (gated by the file's hash). See
[First-run setup](docs/FIRST-RUN-SETUP.md).

## Where to read more

* [First-run setup](docs/FIRST-RUN-SETUP.md)
* [Configuration](docs/CONFIGURATION.md)
* [Integration with AgentBridge](docs/AGENTBRIDGE-INTEGRATION.md)
* [AgentApi plugin architecture](AI.Erp.Plugins.AgentApi/ARCHITECTURE.md)
* Agent-side tool: [ErpTool](https://github.com/Graphene-Lab/ErpTool)
