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

## The ERP works with an AI agent

AI ERP is made to work together with an AI agent. The agent can learn your data structure —
which types exist and which fields they have — and then work on the data itself:

* search the records;
* create new records, change them and delete them;
* connect records to each other (for example, link a project to a customer).

You ask the agent in normal language ("add a customer named ...", "which projects are late?"),
and the agent performs the real operations inside the ERP. Every operation runs as a normal ERP
user, so the same permissions, rules and checks that protect your data from people also apply to
the agent. The agent does not go around them.

## Operated with AgentBridge

The agent is used through **AgentBridge**, the companion program. AgentBridge is where you talk
with the agent and let it work on the ERP. You do not need to use the ERP screens yourself.

## Automatic work

The agent can read the data and change it on its own. Because of this, routine tasks can run
automatically, without a person doing every step by hand. For example, recurring updates to
records can be left to the agent instead of being done manually.

## Related repositories

[WebVella-ERP-StencilJs](https://github.com/WebVella/WebVella-ERP-StencilJs)

[WebVella-ERP-Seed](https://github.com/WebVella/WebVella-ERP-Seed)

[WebVella-TagHelpers](https://github.com/WebVella/TagHelpers)

### Third party libraries

* see [LIBRARIES](https://github.com/WebVella/WebVella-ERP/blob/master/LIBRARIES.md) files

## License

* see [LICENSE](https://github.com/WebVella/WebVella-ERP/blob/master/LICENSE.txt) file

## Contact

#### Developer/Company

* Homepage: [webvella.com](http://webvella.com)
* Twitter: [@webvella](https://twitter.com/webvella "webvella on twitter")
