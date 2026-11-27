# AcuPay runbook

What to do when it is misbehaving. Written to be read at speed, not admired.

## First check, always

```
GET /api/health
```

No token needed. It answers in one of three ways:

| Response | Means | Go to |
|---|---|---|
| 200, `database: Reachable` | The app is up and SQL Server answers | Problem is in a specific endpoint, not the app |
| 503, `database: Unreachable` | The app is up, SQL Server is not answering | Failure 1 |
| No response at all | The app is not running or crashed at startup | Failure 2 |

Every response carries an `X-Correlation-Id` header. Every error body carries the same id. Get that
id from whoever reported the problem and search the logs for it. One search gets you every line
written while that request was handled.

## Failure 1: the database is unreachable

In order:

1. Is SQL Server running? On Windows, `services.msc`, look for SQL Server (MSSQLSERVER).
2. Can you connect with the same credentials from SSMS? If not, it is not the application's fault.
3. Is the connection string set where the app is running? `dotnet user-secrets list` locally.
   A missing one shows at startup as `Connection string 'AcuPay' is not configured`.
4. Did someone change the password or the server name since the last deployment?

## Failure 2: the app will not start

It fails fast and loudly on purpose. Read the first line of the console output.

| Message | Fix |
|---|---|
| `Connection string 'AcuPay' is not configured` | `dotnet user-secrets set "ConnectionStrings:AcuPay" "..."` |
| `Jwt:Key is not configured` | `dotnet user-secrets set "Jwt:Key" "<32+ characters>"` |
| Port already in use | Something else is on 7210. Stop it or change `launchSettings.json` |

## Failure 3: everything returns 401

1. Is the token expired? Lifetime is 60 minutes. Log in again.
2. Was `Jwt:Key` changed since the token was issued? Tokens signed with the old key stop validating
   immediately. Everyone has to log in again. This is expected after a key rotation.
3. Is the `Authorization` header actually being sent, with the `Bearer ` prefix?

## Failure 4: a matching result looks wrong

Check these before assuming the engine is broken:

1. `GET /api/invoices/{id}` and look at `poAmount` and `invoiceAmount`. Most reported bugs are the
   invoice pointing at the wrong PO.
2. Is `AcuPay:PriceTolerancePercent` what you expect in the environment that is running?
3. Run `dotnet test`. If the engine tests pass, the rules are right and the data is wrong.

## Rolling back

Releases are tagged. To go back one:

```bash
git log --oneline --decorate --tags -n 10     # find the previous tag
git checkout v1.0.0
dotnet publish src/AcuPay.Api -c Release -o ./publish
```

Then redeploy `./publish`. A rollback never touches the database. If a release included a database
change, roll the database back first, by hand, before the application.

## What to write down afterwards

Every incident gets three lines in the pull request that fixes it:

1. What broke, from the user's point of view.
2. How you found it. Name the log line or the health response that told you.
3. What you changed, and what would have caught it earlier.
