# AcuPay

A vendor invoice approval API. An invoice is checked against the purchase order it belongs to,
approved automatically when it is small and clean, and routed to a person otherwise.

This is the reference implementation of the AcuPay Lite team project.

## Stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 8 Web API, JWT bearer authentication |
| Rules | Plain C#, no dependencies, unit tested |
| Data | ADO.NET with stored procedures only |
| Database | SQL Server |
| CI | GitHub Actions |

## Running it on a clean machine

1. **Create the database.** In SSMS or Azure Data Studio, create an empty database called `AcuPay`,
   then run these three scripts against it, in order:

   ```
   db/01_schema.sql
   db/02_procedures.sql
   db/03_seed.sql
   ```

   The seed script finishes by listing the six invoices it created. If you see them, the database is ready.

2. **Set your secrets.** Nothing sensitive lives in this repository, so you supply two values yourself:

   ```bash
   cd src/AcuPay.Api
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:AcuPay" "Server=localhost;Database=AcuPay;Trusted_Connection=True;TrustServerCertificate=True;"
   dotnet user-secrets set "Jwt:Key" "change-this-to-at-least-32-random-characters"
   ```

   The application refuses to start if either is missing, with a message telling you which one.

3. **Run it.**

   ```bash
   dotnet run --project src/AcuPay.Api
   ```

   Swagger opens at `https://localhost:7210/swagger`.

4. **Sign in.** `POST /api/auth/login` with `requester1` / `Req@12345`. Copy the token, click
   **Authorize** in Swagger, paste it. Every other endpoint now works.

## Seeded users

| Username | Password | Role |
|---|---|---|
| requester1 | Req@12345 | Requester |
| approver1 | App@12345 | Approver |
| finance1 | Fin@12345 | Finance |

`approver1` created `INV-5003` on purpose, so trying to approve it demonstrates the self-approval block.

## Endpoints

| Method and route | Role |
|---|---|
| `POST /api/auth/login` | anonymous |
| `GET /api/health` | anonymous |
| `GET /api/invoices?status=` | any signed-in user |
| `GET /api/invoices/{id}` | any signed-in user |
| `POST /api/invoices` | Requester, Finance |
| `POST /api/invoices/{id}/match` | Requester, Finance |
| `POST /api/invoices/{id}/approve` | Approver, Finance |
| `POST /api/invoices/{id}/reject` | Approver, Finance |

## The rules

Matching runs in order, first match wins:

| # | Condition | Status | Note |
|---|---|---|---|
| 1 | No PO | Exception | `NO_PO` |
| 2 | PO belongs to another vendor | Exception | `VENDOR_MISMATCH` |
| 3 | Variance above +2.00% | Exception | `OVER_PO_AMOUNT` |
| 4 | Anything else | Matched | `WITHIN_TOLERANCE` |

A matched invoice at or below 50,000 is approved immediately, `DecidedBy` is `SYSTEM` and the note
becomes `AUTO_APPROVED`. Underbilling matches, because the vendor charged less than the PO allowed.

Approval checks, each throwing its own exception type:

- only `Approver` and `Finance` may decide
- nobody may decide an invoice whose `CreatedBy` is their own username
- only `Matched` and `Exception` invoices can be decided
- a rejection needs a reason of at least 10 characters

**The username used in the self-approval check comes from the token claim and from nowhere else.**
Read it from the request body or a header and the rule means nothing.

## Expected result after matching all six seeded invoices

| Invoice | Variance % | Status | Note |
|---|---|---|---|
| INV-5001 | 1.50 | Matched | WITHIN_TOLERANCE |
| INV-5002 | -2.00 | Approved | AUTO_APPROVED |
| INV-5003 | 15.00 | Exception | OVER_PO_AMOUNT |
| INV-5004 | null | Exception | NO_PO |
| INV-5005 | null | Exception | VENDOR_MISMATCH |
| INV-5006 | -10.00 | Approved | AUTO_APPROVED |

INV-5001 stays `Matched` because 101,500 is over the auto-approve limit. INV-5002 sits exactly on the
tolerance boundary and must match rather than fail.

## Configuration

Thresholds live in `src/AcuPay.Api/appsettings.json` under `AcuPay`, and are injected into the rule
classes at startup. There is no literal `2.00` anywhere in the engine.

| Key | Default |
|---|---|
| `AcuPay:PriceTolerancePercent` | 2.00 |
| `AcuPay:AutoApproveLimit` | 50000.00 |
| `AcuPay:MinReasonLength` | 10 |
| `AcuPay:TokenExpiryMinutes` | 60 |

Secrets come from user-secrets locally and from GitHub Actions secrets in CI. `appsettings.json`
holds none, and `appsettings.Development.json` is gitignored.

## CI

`.github/workflows/ci.yml` runs on every push and pull request to `main`: restore, build in Release,
run the tests, publish the API as an artifact. A second job fails the build if a connection string or
a committed `appsettings.Development.json` shows up in the repository.

To prove the build really fails, break one assertion in `tests/AcuPay.Tests`, push it to a branch,
open a pull request, and watch the check go red and the merge button lock.

## Branch protection

`main` requires a pull request, one approving review, and a green CI check. Branches are named
`feature/<owner>-<short-description>`.

## Running on .NET 10

Change `<TargetFramework>` to `net10.0` in all four `.csproj` files and `DOTNET_VERSION` to `10.0.x`
in the workflow. Nothing else changes.

## Tests

```bash
dotnet test
```

17 tests covering the four matching rules, the tolerance and auto-approve boundaries, variance
rounding, and all four approval checks.
