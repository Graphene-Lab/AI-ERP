# Configuration

This page explains how to configure AI-ERP and the agent tool that talks to it.

There are two places to configure:

1. **The ERP itself** — `AI.Erp.Site/config.json`.
2. **The agent tool (ErpTool)** — three environment variables.

## 1. The ERP: `config.json`

File: `AI.Erp.Site/config.json`. It is read when the app starts.

### Database connection

The most important setting is the PostgreSQL connection string.

```json
"ConnectionString": "Server=localhost;Port=5432;User Id=dev;Password=dev;Database=erp;Pooling=true;MinPoolSize=1;MaxPoolSize=100;CommandTimeout=120;Timeout=120;KeepAlive=120;"
```

| Part | Meaning |
|---|---|
| `Server` / `Port` | Where PostgreSQL runs. |
| `User Id` / `Password` | Database login. |
| `Database` | The database name. It can be empty on first run — the setup creates the tables. |
| `Pooling` / `MinPoolSize` / `MaxPoolSize` | Connection pooling. |
| `CommandTimeout` / `Timeout` / `KeepAlive` | Timeouts in seconds. |

### JSON Web Token (JWT)

The agent logs in and gets a JWT. These settings sign and check that token.

```json
"Jwt": {
  "Key": "ThisIsMySecretKeyThisIsMySecretKeyThisIsMySecretKey",
  "Issuer": "webvella-erp",
  "Audience": "webvella-erp"
}
```

- `Key` — the signing key. **Change this before any real deployment.**
- `Issuer` / `Audience` — must match on both sides of the token.

### Other settings

| Key | Meaning |
|---|---|
| `EncryptionKey` | A key used to encrypt some stored values. Change it for production. |
| `Lang` / `Locale` | The default language and locale (for example `en`, `en-US`). |
| `TimeZoneName` | The server time zone. |
| `DevelopmentMode` | `true` shows more detail and errors. Set `false` in production. |
| `EnableBackgroundJobs` | Turn the background job runner on or off. |
| `EmailEnabled` and the `Email*` keys | SMTP settings, only if you use email. |
| `AppName` | The name shown in the app. |

## 2. The agent tool: environment variables

`ErpTool` is configured with three environment variables. It reads them once, on first use.

| Variable | Meaning | Example |
|---|---|---|
| `ERP_BASE_URL` | The base URL of the running ERP. | `http://127.0.0.1:5080` |
| `ERP_USER` | The ERP account the agent logs in as. | `erp@webvella.com` |
| `ERP_PASSWORD` | That account's password. | `erp` |

The agent logs in with these to get a JWT, then calls the API. Every action runs as that
account, so the account's permissions decide what the agent can do.

If the tool is not configured, it returns a clear error and does nothing.

## Security checklist before a real deployment

- Change the admin password (the default is `erp`).
- Change `Jwt.Key` to a long random value.
- Change `EncryptionKey`.
- Set `DevelopmentMode` to `false`.
- Give the agent's account only the permissions it needs.
- Do not put real passwords in a public repository. Use environment variables or a secret store.
