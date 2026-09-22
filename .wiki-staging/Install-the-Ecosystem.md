# Install the AI ERP ecosystem

This guide takes you from an empty machine to a working setup where an AI agent runs your
company inside the ERP. By the end you will have three programs connected:

- **AI ERP** — the ERP that holds your data and runs the real business operations.
- **AgentBridge** — the chat front-end where you talk to the agent.
- **ErpTool** — the plugin that lets the agent drive the ERP.

There are two ways to install:

- **The one-shot installer** (recommended): one script per OS sets everything up on a single
  local machine.
- **Manual**: the step-by-step reference below, also for remote and multi-host setups.

---

## The three parts

| Part | Repo | Job |
|---|---|---|
| **AI ERP** | [Graphene-Lab/AI-ERP](https://github.com/Graphene-Lab/AI-ERP) | Stores the data, runs the real operations, enforces the rules and permissions. |
| **AgentBridge** | [Graphene-Lab/AgentBridge](https://github.com/Graphene-Lab/AgentBridge) | The chat front-end and the agent runtime (AIOrchestrator). You talk here. |
| **ErpTool** | [Graphene-Lab/ErpTool](https://github.com/Graphene-Lab/ErpTool) | Turns the agent's decisions into secure calls to the ERP. Runs inside AgentBridge as a plugin. |

How a request flows:

```
You (plain language)
   ↓
AgentBridge  →  AIOrchestrator (the agent)
   ↓  picks a tool and a method
ErpTool  →  HTTPS + JWT  →  AgentApi  →  AI ERP managers  →  your data
```

The agent never touches the database directly. It acts as a normal ERP user, so the same
permissions and checks that protect your data from people also apply to the agent.

---

## Prerequisites

- **PostgreSQL 14 or newer**, running and reachable from the ERP.
- A machine that can run **.NET 10** apps (Windows or Linux). The released AI ERP archive is
  self-contained, so you do not need to install .NET yourself.
- An **LLM provider** and an API key for AgentBridge (OpenAI, Gemini, DeepSeek, a local model,
  and so on).

---

## Option A — The one-shot installer (recommended)

One script sets up the whole ecosystem on a single local machine: PostgreSQL 16, AI ERP,
AgentBridge and the ErpTool plugin, your company details, and a desktop launcher. The
installer is on the
[releases page](https://github.com/Graphene-Lab/AI-ERP/releases). Download the script for
your OS and run it — you do not need the `.tar.gz` archives by hand, the installer downloads
them for you.

**Windows** — download `install.bat` and run it (double-click it, or run it from a terminal).
It runs `install.ps1` sitting next to it.

**Linux** — download `install.sh` and run:

```bash
bash install.sh
```

**macOS** — download `install.sh` and run `bash install.sh`. It sets up PostgreSQL through
Docker. A native macOS build of the ERP is not published yet, so run the ERP app on Linux or
Docker pointed at that database.

The installer asks for:

1. The company name, legal name, VAT number, address, city, country, email, phone.
2. The default currency (for example EUR) and the timezone.
3. The AI provider and its API key (OpenAI, Anthropic, Gemini, DeepSeek, Mistral, a local
   Ollama model, and others).

It then:

- installs PostgreSQL 16 if it is not already present, and creates the database and role;
- downloads AI ERP, AgentBridge and the ErpTool plugin from the releases;
- writes `config.json` (connection string, JWT key, encryption key) and the company seed in
  `bootstrap.json`;
- configures AgentBridge with your provider and installs the ErpTool plugin under
  `Tools/ErpTool/`;
- starts the ERP and AgentBridge, and installs a desktop / Start Menu launcher (a PWA app
  window).

Everything lands under one install root — `%LOCALAPPDATA%\aierp` on Windows, `~/.aierp` on
Linux/macOS — with logs under `<install root>/logs`. The default admin is
`erp@webvella.com` / `erp`; change it right after the first login.

When it finishes, open the ERP launcher and start talking to the agent.

> The installer targets a single local machine. If you need the ERP and the database on
> different hosts, or a managed cloud database, use the manual install below.

---

## Option B — Manual install

### Step 1 — Install and start AI ERP

1. **Create a PostgreSQL database** the ERP can use, for example:

   ```sql
   CREATE DATABASE erp;
   CREATE USER dev WITH PASSWORD 'dev';
   GRANT ALL PRIVILEGES ON DATABASE erp TO dev;
   ```

2. **Set the connection string** in `AI.Erp.Site/config.json` under `Settings.ConnectionString`:

   ```
   Server=localhost;Port=5432;User Id=dev;Password=dev;Database=erp;Pooling=true;
   ```

3. **Describe your company** in `AI.Erp.Site/bootstrap.json`. This single file is the whole
   first-run setup. See the next section for its shape.

4. **Start the ERP.** On the first start it creates its own tables, a default admin account,
   and everything in `bootstrap.json`. You do not run any setup command.

   ```bash
   dotnet AI.Erp.Site.dll --urls http://127.0.0.1:5080
   ```

5. **Check the setup** with the status endpoint:

   ```bash
   TOKEN=$(curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token \
     -H 'Content-Type: application/json' \
     -d '{"email":"erp@webvella.com","password":"erp"}' \
     | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')

   curl -s http://127.0.0.1:5080/api/v3.0/p/agent/setup-status -H "Authorization: Bearer $TOKEN"
   ```

   A good result looks like:

   ```json
   { "success": true, "data": { "installed": true, "pending": false,
     "entity_count": 30, "seed_counts": { "company": 1, "customer": 6, "product": 12 } } }
   ```

> The default admin (`erp@webvella.com` / `erp`) is for first-run only. Change it before any
> real use. See the Security note at the bottom.

### Step 2 — Install AgentBridge

Download the latest release for your platform from
[Graphene-Lab/AgentBridge/releases](https://github.com/Graphene-Lab/AgentBridge/releases/latest),
or use the one-line installer:

```bash
# Linux / macOS
curl -fsSL https://graphenelab.it/AgentBridge/install.sh | bash
```

```powershell
# Windows (PowerShell)
irm https://graphenelab.it/AgentBridge/install.ps1 | iex
```

Start AgentBridge and set your LLM provider and API key (the guided setup in the TUI, or the
provider config). See the AgentBridge README.

### Step 3 — Install the ErpTool plugin

1. Download the ErpTool release zip from
   [Graphene-Lab/ErpTool/releases](https://github.com/Graphene-Lab/ErpTool/releases/latest).

2. Copy its contents into the `Tools/ErpTool/` folder next to the AgentBridge program:

   ```
   <AgentBridge install>/Tools/ErpTool/ErpTool.dll  (+ its files)
   ```

   AgentBridge finds the plugin at startup, or adds it while it is running (hot-add, about 30
   seconds).

3. **Tell the tool how to reach the ERP.** Either set three environment variables before you
   start AgentBridge:

   | Variable | Example |
   |---|---|
   | `ERP_BASE_URL` | `http://127.0.0.1:5080` |
   | `ERP_USER` | `erp@webvella.com` |
   | `ERP_PASSWORD` | `erp` |

   or write the file `PersistentData/erp.json` in the AgentBridge folder:

   ```json
   { "baseUrl": "http://127.0.0.1:5080", "user": "erp@webvella.com", "password": "erp" }
   ```

4. **Turn the tool on.** In the AgentBridge TUI, open **Settings → Tools** (the `/tools`
   command), find **ErpTool** in the list, and tick it. Save. The agent now sees the ERP
   methods.

### Step 4 — Try it

In AgentBridge, ask the agent:

> "List the ERP entities."

The agent calls `get_schema` and shows the tables. If it answers, the whole chain works.

---

## Remote and multi-host (enterprise) setups

The one-shot installer puts everything on one machine. In a company you often want the parts
on different hosts. The manual install supports this directly — the ERP talks to PostgreSQL and
AgentBridge talks to the ERP only over the network, so each piece can live wherever you like.

**ERP and PostgreSQL on different hosts.** Point the connection string at the database host
instead of `localhost`:

```
Server=db.internal;Port=5432;User Id=aierp;Password=<strong>;Database=aierp;Pooling=true;
```

- Open the PostgreSQL port between the two hosts (firewall / security group) and restrict it to
  the ERP host's address.
- Use a strong password. For traffic that leaves a trusted network, enable TLS in PostgreSQL
  (`sslmode=require` in the connection string).
- The ERP creates its own schema, types and casts on first run, so the role needs to own the
  database or have rights to create types and casts (a superuser role is simplest).

**ERP behind a reverse proxy / public URL.** Bind the ERP to all interfaces and put a proxy in
front with HTTPS:

```bash
dotnet AI.Erp.Site.dll --urls http://0.0.0.0:5080
```

Put nginx, Caddy, or IIS in front, terminate TLS there, and forward to `127.0.0.1:5080` (or
the ERP host). The ERP uses the request host, so links and the PWA follow the public URL. Keep
the ERP port closed to the public internet; only the proxy should be reachable.

**AgentBridge on a separate host.** Tell the ErpTool plugin where the ERP lives with
`ERP_BASE_URL` (or `PersistentData/erp.json`), set to the ERP's reachable URL, for example
`https://erp.example.com`. The agent then drives that ERP over HTTPS + JWT, exactly as if it
were local.

**Managed cloud PostgreSQL (RDS, Azure Database, Supabase, Neon, ...).** Use the connection
string the provider gives you. The ERP builds its own schema on first run. Confirm the role can
create types and casts; some managed services restrict superuser, so grant the database owner
role or the specific `CREATE`/`CREATE TYPE`/`CREATE CAST` rights.

**Running as a service.** For anything beyond a single desktop, run the ERP and AgentBridge as
managed services so they restart on boot and survive crashes:

- Linux: a `systemd` unit per process (`AI.Erp.Site`, `agent`), with the connection string and
  keys supplied through the unit's environment or a protected config file.
- Windows: a Task Scheduler task or a service wrapper for `AI.Erp.Site.exe` and `agent.exe`.

**Hardening before a real deployment.** Change the default admin password, the JWT key, and the
encryption key in `config.json`. Keep credentials out of source control — use environment
variables or files that are not part of the program archive.

---

## The company setup file (`bootstrap.json`)

`bootstrap.json` is the single file that describes your company and the data model it needs.
The ERP reads it on first start and creates everything. It has two parts: `entities` (the
tables and fields) and `seed` (the starting records).

The company itself is one seeded record:

```json
{
  "version": 4,
  "entities": [
    { "name": "company", "label": "Company", "labelPlural": "Companies",
      "fields": [
        { "name": "name", "type": "text", "label": "Name", "required": true, "unique": true },
        { "name": "legal_name", "type": "text", "label": "Legal name" },
        { "name": "vat_number", "type": "text", "label": "VAT number" },
        { "name": "address", "type": "multiline", "label": "Address" },
        { "name": "city", "type": "text", "label": "City" },
        { "name": "country", "type": "text", "label": "Country" },
        { "name": "email", "type": "email", "label": "Email" },
        { "name": "phone", "type": "phone", "label": "Phone" },
        { "name": "currency", "type": "text", "label": "Currency" },
        { "name": "default_warehouse_id", "type": "guid", "label": "Default warehouse" }
      ] }
  ],
  "seed": {
    "company": [
      { "name": "Northwind Traders", "legal_name": "Northwind Traders S.r.l.",
        "vat_number": "IT01234560123", "address": "Via Roma 10", "city": "Milan",
        "country": "Italy", "email": "info@northwind.example", "phone": "+39 02 1234567",
        "currency": "EUR", "default_warehouse_id": "@warehouse:WH1" }
    ]
  }
}
```

A link to another record is written as `@entity:uniqueValue` — for example
`"@warehouse:WH1"` or `"@payment_term:TP30"`. The setup resolves it to the real id when it
applies the file.

The file also seeds the master data a company needs to start: VAT codes, payment terms,
warehouses, customers, suppliers, products, price lists and more.

**Safe to restart.** The setup is gated by the SHA-256 hash of the file. If the file has not
changed, the run is skipped. If you edit it, the change is applied on the next start. It only
adds; it never deletes or renames. To start clean, drop and recreate the database.

**Apply a change without a restart:** edit `bootstrap.json`, then call
`POST /api/v3.0/p/agent/reprovision` (the ErpTool `reprovision` method).

---

## Example prompts

Once the tool is on, talk to the agent in plain language. A few examples:

- "Add a customer named Rossi & Figli, email rossi@example.com, category wholesale."
- "Create a quote for 10 of product SKU-1001 and 5 of SKU-1002."
- "Turn quote Q-2026-0001 into an order."
- "Deliver order SO-1042 and then invoice it."
- "Record a payment of 1,200 against invoice INV-2026-0007."
- "Raise a purchase request for 50 units of SKU-2002 from supplier Alfa."
- "Receive the goods for purchase order PO-330 and register the supplier bill."
- "Show the receivables aging."
- "Which products are below their minimum stock?"

The agent reads the schema, picks the right method, and runs the real operation inside the ERP.

---

## Daily scheduling examples

AgentBridge can run work on a schedule. Combined with the ERP, routine jobs run by themselves:

- "Every morning at 8, invoice everything that was delivered yesterday and tell me the total."
- "Every day, check stock and warn me about any product below its minimum."
- "Every Monday, send me the receivables aging and list the overdue invoices."
- "Every month, run a cycle count for warehouse WH1."

You set the cadence in plain language; the agent schedules the job, runs it on time, and
records the outcome.

---

## Security

- The agent acts as the account in `ERP_USER`. Give that account only the permissions it needs.
- Change the default admin password and the JWT key before a real deployment.
- Keep the ERP credentials out of public repositories. Use environment variables or the
  `PersistentData/erp.json` file, which is not part of the program archive.

---

## Troubleshooting

| Symptom | What to check |
|---|---|
| Tool says "no ERP connection is configured" | Set `ERP_BASE_URL`, `ERP_USER`, `ERP_PASSWORD`, or `PersistentData/erp.json`. |
| `setup-status` shows `installed: false` | The ERP has not finished first-run setup. Check the ERP log and that `bootstrap.json` is next to `config.json`. |
| Agent does not see ErpTool methods | The plugin is not in `Tools/ErpTool/`, or it is not ticked in `/tools`. |
| ERP will not start | PostgreSQL is not reachable, or the connection string in `config.json` is wrong. |
| `pending: true` in setup-status | You edited `bootstrap.json`; call `reprovision` or restart the ERP to apply it. |
