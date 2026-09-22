# AI ERP — Wiki

Welcome to the **AI ERP** wiki. AI ERP is a free, open-source platform to manage business data:
customers, projects, documents, tasks, and anything else a company needs. You can adapt it to
your own way of working without writing code.

It is built on **.NET 10 (ASP.NET Core)** and stores data in **PostgreSQL**. It is a fork of
[WebVella ERP](https://github.com/WebVella/WebVella-ERP), renamed to `AI.Erp`, with an added
layer that lets an AI agent drive the ERP.

## What makes it different

An AI agent can read the data structure and work on the data itself: search records, create and
change them, link them, and run whole business operations in one step (place an order, deliver
it, invoice it, collect a payment). Every operation runs as a normal ERP user, so the same
permissions and checks apply to the agent.

## Pages

- [Install the ecosystem](Install-the-Ecosystem) — set up the whole agentic system: AI ERP +
  PostgreSQL + the company setup file + AgentBridge + the ErpTool plugin, with example prompts
  and daily scheduling. A one-shot installer (`.bat` / `.sh`) does it on one machine; the page
  also covers manual and remote / multi-host setups.
- [AI Integration](AI-Integration) — how the AI agent drives the ERP (AgentApi, ErpTool, AgentBridge), with a diagram and the security model.

For the full developer reference (entities, pages, tag helpers, EQL, plugins, hooks), see the
[`docs/developer/`](https://github.com/Graphene-Lab/AI-ERP/tree/master/docs/developer) folder
in the repository.

## Source of truth

This wiki is verified against the actual source code. Where the upstream vendor documentation is
approximate, the pages here are corrected to match what the code really does. The source code is
the primary source.
