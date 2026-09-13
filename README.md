[![Project Homepage](https://img.shields.io/badge/Homepage-blue?style=for-the-badge)](https://webvella.com)
[![Dotnet](https://img.shields.io/badge/platform-.NET-blue?style=for-the-badge)](https://www.nuget.org/packages/AI.Erp)
[![GitHub Repo stars](https://img.shields.io/github/stars/WebVella/WebVella-ERP?style=for-the-badge)](https://github.com/WebVella/WebVella-ERP/stargazers)
[![Nuget version](https://img.shields.io/nuget/v/AI.Erp?style=for-the-badge)](https://www.nuget.org/packages/AI.Erp)
[![Nuget download](https://img.shields.io/nuget/dt/AI.Erp?style=for-the-badge)](https://www.nuget.org/packages/AI.Erp)
[![License](https://img.shields.io/badge/license-Apache--2.0-green?style=for-the-badge)](https://github.com/Graphene-Lab/AI-ERP/blob/master/LICENSE.txt)

---

# AI ERP

**AI ERP** is a free, open-source program to manage business data: customers, projects,
documents, tasks, and anything else a company needs. You can adapt it to your own way of
working without writing code.

It is built on the current .NET platform (**ASP.NET Core 10 / .NET 10**) and stores data in a
**PostgreSQL 16** database. It runs on Windows and on Linux (tested on Windows).

If you like this project and want it to continue, you can support it by:

* giving it a "star";
* contributing to the source;
* becoming a Sponsor: click the Sponsor button. Thank you in advance.

## Why an AI-driven ERP

A fully automated ERP is no longer a distant idea. The key is an **AI agent** that treats the ERP
like a tool — a digital worker that does not need a person to click buttons or type data. It
uses the same information and the same rules that a human employee would use.

A traditional ERP is powerful but complex. People must move through many screens, remember the
steps, and spend hours on repetitive work. An AI agent takes that busywork away. In AI ERP the
agent first reads your data model — which types exist and which fields they have — and then works
the whole cycle for you:

* **Sales:** create a quote, turn it into an order, deliver it, issue the invoice, and record the
  payment. Each step is checked against the one before it, so you cannot deliver more than you
  sold or invoice what was never delivered.
* **Purchasing:** raise a purchase request, turn it into an order, receive the goods, match the
  supplier bill to the order and the receipt, and pay it.
* **Stock:** keep the per-warehouse stock and the movement ledger in sync, move stock between
  warehouses, reserve it for an order, and run a cycle count.

This is not a demo. These are real operations that run inside the ERP, through the same API and
the same permissions a person uses. The agent cannot create a record it is not allowed to create,
or delete one it cannot delete. The same checks that protect your data from people also apply to
the agent.

The point is not only speed. It is about freeing people. When the agent takes the repetitive,
rule-based steps, employees spend their time on judgment, relationships, and the problems that
need a human touch. The ERP stops being a tool that people struggle to use and becomes a partner
that works in the background, handled by an agent that understands the business.

## The ERP works with an AI agent

AI ERP is made to work together with an AI agent. The agent can learn your data structure —
which types exist and which fields they have — and then work on the data itself:

* search the records;
* create new records, change them and delete them;
* connect records to each other (for example, link a project to a customer);
* run whole business operations in one step — place an order, deliver it, invoice it and
  collect a payment — or the full purchase-to-pay cycle;
* read reports, such as sales by customer or product and the receivables aging.

You ask the agent in normal language ("add a customer named ...", "which projects are late?"),
and the agent performs the real operations inside the ERP. Every operation runs as a normal ERP
user, so the same permissions, rules and checks that protect your data from people also apply to
the agent. The agent does not go around them.

## Operated with AgentBridge

The agent is used through **AgentBridge**, the companion program. AgentBridge is where you talk
with the agent and let it work on the ERP. You do not need to use the ERP screens yourself.

## Documentation

* [Architecture](ARCHITECTURE.md) — how the whole system is put together.
* [First-run setup](docs/FIRST-RUN-SETUP.md) — install and configure the ERP with no human installer.
* [Configuration](docs/CONFIGURATION.md) — the ERP settings and the agent tool settings.
* [Integration with AgentBridge](docs/AGENTBRIDGE-INTEGRATION.md) — how the agent drives the ERP through ErpTool.
* [AgentApi architecture](AI.Erp.Plugins.AgentApi/ARCHITECTURE.md) — the REST API and the fork/vendor build.

The agent-side tool is [ErpTool](https://github.com/Graphene-Lab/ErpTool).

## Related repositories

This fork builds on three upstream WebVella repositories. We consume them as build artifacts,
not as source inside this tree.

| Repository | What it is | How this project uses it |
|---|---|---|
| [WebVella-TagHelpers](https://github.com/WebVella/TagHelpers) | ASP.NET Core Razor TagHelper library (Bootstrap-based) | Consumed as the NuGet package `WebVella.TagHelpers` (see `AI.Erp.Web.csproj`). |
| [WebVella-ERP-StencilJs](https://github.com/WebVella/WebVella-ERP-StencilJs) | StencilJS web components (`wv-*`) for the ERP UI | Consumed as prebuilt JS bundles under each plugin's `wwwroot/js/`. |
| [WebVella-ERP-Seed](https://github.com/WebVella/WebVella-ERP-Seed) | A starter seed project for a WebVella ERP site | Not used directly. This fork ships its own seed in `bootstrap.json`. |

We do not vendor the source of these repositories, and we do not use git submodules. If a
component must be customized, the path is to fork that repository, rebuild it, and drop the
new artifact into this project.

### Third party libraries

* see [LIBRARIES](LIBRARIES.md) for the full list of third-party libraries and the AI-agent
  integration diagram.

## License

* see [LICENSE](https://github.com/WebVella/WebVella-ERP/blob/master/LICENSE.txt) file

## Contact

#### Developer/Company

* Homepage: [webvella.com](http://webvella.com)
* Twitter: [@webvella](https://twitter.com/webvella "webvella on twitter")
