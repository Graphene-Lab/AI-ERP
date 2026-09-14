# First-Run Setup (No Human Installer)

## What this is

AI-ERP can be installed and configured by an AI agent with no human at the keyboard.
The ERP itself has no interactive wizard: it sets itself up on the first start.
You only need a running database and one JSON file that describes the company.

> To prepare that JSON file and the rest of the ecosystem (AgentBridge + the ErpTool plugin)
> in one go, use the **setup wizard** (`tools/setup-wizard.sh` / `tools/setup-wizard.ps1`).
> It writes the company record into `bootstrap.json` for you. Full guide:
> [Install the ecosystem](https://github.com/Graphene-Lab/AI-ERP/wiki/Install-the-Ecosystem).

## Prerequisites

- A PostgreSQL database that is running and reachable from the app.
- The connection string is set in `AI.Erp.Site/config.json` (key `Settings.ConnectionString`).
- The app is built and runnable.

Example connection string:

```
Server=localhost;Port=5432;User Id=dev;Password=dev;Database=erp;Pooling=true;
```

No tables and no data are needed. The system creates them.

## Step 1: Start the ERP

When the ERP starts, it creates its own system tables and a default admin account.
Nothing blocks the start. You do not run any setup command.

The default admin account is:

| Field    | Value              |
|----------|--------------------|
| Email    | `erp@webvella.com` |
| Password | `erp`              |
| Username | `administrator`    |

Log in to get a JWT token:

```
POST /api/v3/en_US/auth/jwt/token
Content-Type: application/json

{
  "email": "erp@webvella.com",
  "password": "erp"
}
```

The response contains the token. Use it in later calls as a bearer token.

> These are default development credentials. You must change the password before a real
> deployment. See the Security note below.

## Step 2: The `bootstrap.json` file

`bootstrap.json` is the single first-run custom file. It is placed in the `AI.Erp.Site`
project and copied next to the running app. It describes the company and the business
data model the company needs. On the first start, the ERP reads it and creates everything.

File location: `AI.Erp.Site/bootstrap.json`

The file has two main parts:

- `entities` — the tables and their fields.
- `seed` — the starting records for each table.

### Small example

```json
{
  "version": 1,
  "entities": [
    {
      "name": "customer",
      "label": "Customer",
      "labelPlural": "Customers",
      "fields": [
        { "name": "name", "type": "text", "label": "Name", "required": true, "unique": true },
        { "name": "email", "type": "email", "label": "Email" },
        { "name": "category", "type": "select", "label": "Category",
          "options": ["retail", "wholesale", "vip"], "defaultValue": "retail" },
        { "name": "credit_limit", "type": "number", "label": "Credit Limit",
          "decimalPlaces": 2, "minValue": 0 }
      ]
    }
  ],
  "seed": {
    "customer": [
      { "name": "Alpine Coffee Roasters", "email": "orders@alpinecoffee.example",
        "category": "wholesale", "credit_limit": 20000 }
    ]
  }
}
```

### `entities`

Each entity is one table:

- `name` — the table name (lower-case, used in the API).
- `label` — the display name for one record.
- `labelPlural` — the display name for many records.
- `fields` — the list of columns.

Each field:

- `name` — the column name.
- `type` — the field type (see below).
- `label` — the display name.
- `required` — true if the value must be present.
- `unique` — true if the value must be different in every record.
- `options` — for a `select` field, the allowed values.
- `defaultValue` — the value used when none is given.

### Field types

| Type       | Meaning                                   |
|------------|-------------------------------------------|
| `text`     | A short single-line text.                 |
| `multiline`| A long text block.                        |
| `email`    | An email address.                         |
| `phone`    | A phone number.                           |
| `url`      | A web address.                            |
| `number`   | A number. Use it for money too.          |
| `percent`  | A percentage value.                       |
| `bool`     | A true / false value.                     |
| `date`     | A date (no time).                         |
| `datetime` | A date with a time.                       |
| `guid`     | A unique id. Use it for a link to another record. |
| `select`   | One value chosen from `options`.          |

### Relationships

A link to another record is a `guid` field. You set the id of the other record directly
on the row. This is simple for an agent to use. For example, a sales order line has a
`product_id` field of type `guid`. It holds the id of the product.

### `seed`

`seed` holds the starting records. It is a map of entity name to a list of records.
Each record sets values by field name. The seed only adds master data. It does not need
every field, only the ones you want to set.

## Step 3: How it is applied

You do not run a command. You only start the app.

The `AgentApi` plugin (`AI.Erp.Plugins.AgentApi`) applies the file during its
`Initialize` hook. The host already runs this hook inside a system security scope, so
the plugin can create tables and records freely. The code is `Bootstrap.Apply()`.

In short: put `bootstrap.json` next to the app, start the app, and the setup runs.

## Safe restarts (idempotency)

The setup is safe to restart. It will not create duplicates.

- The whole run is gated by the SHA-256 hash of `bootstrap.json`. The hash is stored in
  the database (plugin data, key `bootstrap`).
- If the file has not changed since it was applied, the run is skipped.
- Each entity is also checked for existence before it is created.
- Each seed record is checked by its unique field before it is added.
- If any step fails, the marker is **not** saved. So the next start retries.

To force a clean re-apply: drop the database and recreate it, then start the app again.

## Changing the setup later

Edit `bootstrap.json`. Because of the hash gate, a changed file is applied on the next
start. New entities and new seed records are added. Existing ones are skipped.

Note: the setup only adds. It does not delete or rename existing entities or fields.
For a clean slate, drop and recreate the database, then start the app.

## Check and re-apply the setup (no restart)

You or the agent can check the setup state and re-apply it without restarting the app.

- `GET api/v3.0/p/agent/setup-status` — returns whether the setup is applied, the number of
  business entities, the record count of each seeded entity, and whether a change is pending
  (the file hash differs from the applied hash).
- `POST api/v3.0/p/agent/reprovision` — applies `bootstrap.json` now. It is idempotent: if
  the file has not changed, it does nothing and says so. Use it after editing the file to push
  new entities or seed without a restart.

In `ErpTool` these are the `SetupStatus()` and `Reprovision()` methods.

## Security note

The default admin account uses a weak password (`erp`). This is only for first-run and
development. In a real deployment, change the admin password after the first login.
Also change the JWT key and the database password in `config.json`.

## The AI agent side

An external agent uses a tool called `ErpTool`. It talks to the ERP over a JWT-secured
REST API (`api/v3.0/p/agent/...`). The agent logs in with the admin account. Then it can:

- Discover the schema (entities and fields).
- Query records.
- Create, update, and delete records.
- Call composed business operations.

## Quick reference: the 10 entities

| Entity               | What it holds                                              |
|----------------------|-----------------------------------------------------------|
| `company`            | The company itself (name, VAT, address, currency).         |
| `customer`           | A customer (contact, address, category, credit limit).     |
| `product`            | A product (SKU, price, cost, stock, active flag).          |
| `supplier`           | A supplier (contact and address).                          |
| `sales_order`        | A sales order header (customer, dates, status, total).     |
| `sales_order_line`   | One line of a sales order (product, quantity, price).      |
| `purchase_order`     | A purchase order header (supplier, dates, status, total).  |
| `purchase_order_line`| One line of a purchase order (product, quantity, cost).    |
| `invoice`            | An invoice (customer, dates, amount, status).              |
| `payment`            | A payment against an invoice (amount, date, method).       |

Seed data in the current file: 1 company, 6 customers, 12 products, 4 suppliers, plus the
supporting master data (VAT codes, payment terms, warehouses, price lists, exchange rates and
more). Call `setup-status` to see the exact count of every seeded entity.
