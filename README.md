# BetaFleet Job File

Full-stack implementation of the BetaFleet Job File prototype. A job file is opened once. Clearing & Forwarding, Warehousing, Distribution & Courier and Transit Transport each work their leg of it. Hand-overs, costs, documents, trips and invoices all roll up to the same file.

| Layer | Stack |
| --- | --- |
| API | ASP.NET Core 11 minimal APIs, EF Core 11, Npgsql |
| Database | PostgreSQL 19 |
| Web | React 19, React Router 8, TanStack Query 5, Vite 8, TypeScript 7 |
| Runtime | Docker Compose (postgres, api, web via nginx) |

## Run it

```powershell
docker compose up -d --build
```

| URL | What |
| --- | --- |
| http://localhost:5181 | Web app |
| http://localhost:5181/docs | API reference (Scalar / OpenAPI) |
| http://localhost:5181/health | Health check |
| localhost:5434 | PostgreSQL (`betafleet` / `betafleet`) |

The ports don't clash with BetaWMS or the TMS demo. On first start the API applies EF migrations and loads the prototype's sample data. To start again from clean data:

```powershell
docker compose down -v; docker compose up -d --build
```

## Modules

- **Job files**: control tower, job list, job 360 (legs, timeline, documents, charges and margin, cargo and trips, hand-overs), open-job wizard, hand-over queue.
- **Clearing & Forwarding**: six-stage customs entries (release automatically hands over to the next unit), container tracking with ISO 6346 check digits and 14-day detention.
- **Transit**: route master with standard trip costs, filling masters (draft → approval → disbursement → shipment order), truck fitness checks, live tracking, GPS alerts, POD.
- **Finance**: billing (consolidated or per unit, VAT on services only, credit-limit warning), invoices queued to SAP through the integration bridge, two-level expense approval, profitability by job, unit and customer.
- **Customer portal**: customer view of files, trips, documents and invoices; transport requests that ops review and turn into job files.
- **Fleet**: vehicle and driver register, compliance expiries, workshop (inspection → job card → parts → close), tyre lifecycle, driver app.

## Business rules enforced by the API

- Filling-master lines more than 5% off the route standard need a reason before submission.
- A truck can't be submitted, approved or dispatched if it has an open job card, an overdue service, a tyre under 4 mm, or an expired truck or driver document.
- Expenses over $1,000 need Finance approval after the unit head. Requesters can't approve their own. Rechargeable expenses become disbursement lines once paid.
- Freight becomes billable only after the last POD on the job. A charge can't be invoiced twice.
- A job card closes only when every task is done, and a task can't be marked done while its spare part is still waiting at the store.

## Sign-in and roles

Users sign in with a username and password and get a JWT that lasts 10 hours. The API takes the user's identity only from that signed token. Every `/api` route needs it except `/api/auth/login`.

- **Passwords:** hashed with ASP.NET Identity's PBKDF2 hasher. They must be at least 10 characters and include letters and digits.
- **Lockout:** 5 wrong passwords lock the account for 15 minutes. Sign-in is also limited to 30 attempts per minute per IP.
- **Revocation:** changing a password, an admin reset, a role change or disabling the account cancels the user's existing tokens straight away.
- **Temporary passwords:** new accounts and admin resets get one. Until the user changes it, the account can only reach the change-password screen.
- **Customer accounts:** linked to one customer. They can only see that customer's portal and request transport for it.
- **Driver accounts:** can only see and update their own trips.
- **Staff roles:** clerk, ops manager, unit head, finance, workshop, storekeeper and admin. Each write endpoint checks the role (`Actor.Require`). Admins manage accounts under **Users**.

**Local demo:** on an empty database, docker compose creates one account per role, all sharing `DEMO_PASSWORD`. The default is in `docker-compose.yml`.

| Username | Role |
| --- | --- |
| `admin` | Administrator |
| `jkimaro` | Ops clerk |
| `smassawe` | Ops manager |
| `asaid`, `mjuma` | Unit heads |
| `lmrema` | Finance |
| `jmollel` | Workshop |
| `nkisanga` | Storekeeper |
| `pbanda`, `sngowi` | Drivers |
| `kmc.portal` | Customer (Kilimanjaro Mining Company) |
| `everdigitio.portal` | Customer (Everdigitio Ltd) |

**Production:** in `.env`, set `JWT_KEY` (`openssl rand -base64 48`), leave `DEMO_PASSWORD` empty and set `ADMIN_PASSWORD`. The first start creates only `admin`, who must change that password at first sign-in and then creates everyone else. Serve it over HTTPS (behind your reverse proxy), because the password and token go over the wire.

## SAP Business One

The API never calls SAP directly. `SapBridgeWorker` picks up queued invoices and posts them to the integration bridge at `SAP_BRIDGE_URL` (`POST {url}/api/ar-invoices`). Each line carries the job file and unit as cost-centre dimensions. If `SAP_BRIDGE_URL` is empty and `SAP_DEMO_MODE=true`, invoices are marked posted with a demo A/R number.

## Development

```powershell
docker compose up -d postgres api
cd frontend; npm install; npm run dev
```

The web app runs at http://localhost:5181 with `/api` proxied to the API container. To change the schema, add a migration in a .NET 11 SDK container:

```powershell
docker run --rm -v "${PWD}/backend:/src" mcr.microsoft.com/dotnet/sdk:11.0 bash /src/ef.sh <MigrationName>
```
