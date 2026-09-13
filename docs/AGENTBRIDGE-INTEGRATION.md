# Integration with AgentBridge

AI-ERP is driven by an AI agent through **AgentBridge**, using a tool called **ErpTool**.
This page explains how the pieces connect and how to set it up.

## The pieces

| Piece | What it is |
|---|---|
| **AgentBridge** | The companion program where you talk to the agent. You type a request in plain language. |
| **AIOrchestrator** | The agent loop inside AgentBridge. It reads your request, picks a tool, calls it, and answers. |
| **ErpTool** | The tool the agent uses to act on the ERP. It is a plugin for AIOrchestrator. |
| **AgentApi** | A plugin inside AI-ERP that exposes the ERP over a JWT-secured REST API. |
| **AI-ERP** | The ERP itself. It runs the real operations and enforces the rules. |

## How a request flows

```
You (plain language)
   ↓
AgentBridge  →  AIOrchestrator (the agent)
   ↓  picks a tool and a method
ErpTool  →  HTTP + JWT  →  AgentApi  →  AI-ERP managers  →  your data
```

1. You ask the agent something, for example "add a customer named Rossi" or "show the
   receivables aging".
2. The agent reads the ERP schema, picks the right ErpTool method, and calls it.
3. ErpTool sends an HTTPS request to the AgentApi, with a JWT it got by logging in.
4. The AgentApi runs the call through the ERP's own managers. The ERP checks the rules and the
   permissions of the logged-in account, then does the work.
5. The result comes back and the agent answers you.

The agent never touches the database directly. It acts as a normal ERP user, so the same
permissions and checks that protect your data from people also apply to the agent.

## What the agent can do

Through ErpTool the agent can:

- **Learn the model** — list every entity and field before it acts.
- **Read and write records** — create, update, delete, and query with EQL.
- **Run business operations** — one call does many steps: place an order, deliver it,
  invoice it, collect a payment; or the whole purchase-to-pay cycle (order, receive, bill, pay).
- **Manage stock** — transfer, adjust, reserve and release.
- **Read reports** — sales by customer or product, purchases by supplier, receivables aging.
- **Check and re-apply the setup** — see if the ERP is installed and push a setup change
  without a restart.

The full method list is in the [ErpTool repository](https://github.com/Graphene-Lab/ErpTool).

## Setup

1. **Run the ERP.** Start AI-ERP and let it finish its first-run setup (see
   [First-run setup](FIRST-RUN-SETUP.md)). Note the URL, for example `http://127.0.0.1:5080`.
2. **Configure the tool.** Set the three environment variables that ErpTool reads:

   | Variable | Value |
   |---|---|
   | `ERP_BASE_URL` | The ERP URL, e.g. `http://127.0.0.1:5080` |
   | `ERP_USER` | The ERP account, e.g. `erp@webvella.com` |
   | `ERP_PASSWORD` | That account's password |

3. **Add the tool to AgentBridge.** Load ErpTool as a tool plugin in AgentBridge. The agent
   now sees the ERP methods and can use them.
4. **Talk to the agent.** Ask it to do something in the ERP.

See [Configuration](CONFIGURATION.md) for the full list of settings.

## Security

- The agent acts as the account in `ERP_USER`. Give that account only the permissions it needs.
- Change the default password and the JWT key before a real deployment.
- Keep the ERP credentials out of public repositories. Use environment variables or a secret store.
