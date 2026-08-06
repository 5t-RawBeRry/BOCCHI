# BOCCHI Coffer Observation API

Cloudflare Worker that accepts opt-in anonymous treasure-coffer observations from BOCCHI.
Payload shape matches AOCC (`POST /api/v1/observations`) so the plugin URL can point at either API.

Unlike AOCC’s pot-reveal-only filter, this API accepts **any positive coffer `dataId`** in Occult Crescent territories (**1252** South Horn, **1346** North Horn).

Also hosts **pot-cycle sync** (`/api/v1/pot-cycles`) so BOCCHI clients can share Magic Pot spawn anchors per instance.

## Local setup

```powershell
cd cloudflare/coffer-api
npm install
npm run db:migrate:local
npm run dev
```

- `GET http://localhost:8787/health`
- `POST http://localhost:8787/api/v1/observations`
- `GET http://localhost:8787/api/v1/candidates?territoryId=1252` (accepted catalog for hunt routing)
- `POST http://localhost:8787/api/v1/pot-cycles`
- `GET http://localhost:8787/api/v1/pot-cycles?instanceKey=...`

## Deploy

```powershell
npx wrangler login
npx wrangler d1 create bocchi-coffer-observations
# Paste the returned database_id into wrangler.jsonc → d1_databases[0].database_id
npm run db:migrate:remote
npm run deploy
```

Copy into BOCCHI is not required — the plugin posts to:

`https://bocchi-coffer-api.kagekazu.workers.dev/api/v1/observations`

(Users only see the opt-in checkbox; the URL is hardcoded.)

Optional admin token:

```powershell
npx wrangler secret put ADMIN_TOKEN
```

## Privacy

Submissions are opt-in. Stored fields are territory, coffer data id, world coordinates, coffer type label, anonymous installation hash, plugin version, and observed time. No character or account names.

Pot-cycle rows store an instance fingerprint hash, territory, datacenter id, pot fate id, spawn unix time, installation hash, plugin version, and observed time — still no character or account names.
