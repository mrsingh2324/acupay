# Database scripts

Run in order against an empty database called `AcuPay`.

| Script | What it does |
|---|---|
| `01_schema.sql` | Drops and recreates the four tables, with keys, constraints and the queue index |
| `02_procedures.sql` | The six stored procedures the application calls. Nothing else touches the tables |
| `03_seed.sql` | Clears and reloads the three users, three vendors, four POs and six invoices |

All three are re-runnable. Running them again resets you to a known state, which is what you want
before working through the test script.

## Password hashes

The seed hashes are PBKDF2-SHA256, 100,000 iterations, stored as `iterations.salt.key` in base64.
They were produced by the same algorithm as `PasswordHasher.Hash` in the API project, so they verify
against it directly.

| Username | Password |
|---|---|
| requester1 | Req@12345 |
| approver1 | App@12345 |
| finance1 | Fin@12345 |

To generate a hash for a new password, call `PasswordHasher.Hash("...")` from a throwaway console
app or a unit test and paste the result into the seed script.

## Least privilege

The login the application uses needs `EXECUTE` on the six procedures and nothing more. It does not
need `db_owner`, and it does not need `SELECT` on the tables.

```sql
CREATE USER acupay_app FOR LOGIN acupay_app;
GRANT EXECUTE ON SCHEMA::dbo TO acupay_app;
```
