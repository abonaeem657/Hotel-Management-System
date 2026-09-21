# Hotel regression checks

From the solution directory:

```powershell
dotnet run --project tests/CustomerBookingChecks -- hotel
```

No test framework packages are required. Uses the app's configured SQL Server (appsettings plus
environment overrides) but creates a uniquely named `HotelCustomerChecks_<guid>` database, which is
deleted in `finally`. The configured database is never modified. SQL credentials need permission
to create/drop this disposable database. No migrations are created or applied.

The harness serves the real MVC controllers and compiled Razor views on an ephemeral loopback port.
It exercises the real Register/Login/Logout endpoints with temporary Identity users and randomly
generated test passwords. It also tests idempotent role seeding and the development-only Admin seed.
No credentials are printed or installed in the configured hotel database.

Checks pricing, validation, unfiltered catalog, minimal Home, public and admin rendering, anti-forgery, authentication,
overposting, booking creation, status-based overlap rules, adjacent stays, owner-only My Bookings,
and simultaneous conflicting submissions. Additional checks cover registration role tampering,
generic login failures, Remember Me, safe redirects, logout, Admin access, and role navigation.
The added ManagementChecks cover Admin dashboard counts and Completed-only revenue, authorization
and anti-forgery, every booking lifecycle transition, required rejection reasons, concurrent approval
conflicts, adjacent approvals, booking filters, seasonal pricing and offers CRUD with live pricing
integration, inventory CRUD/quantity validation and low-stock boundaries, public Contact submission,
server-owned date/read flags, inbox read/delete and HTML encoding, RoomService overposting,
room-history deletion protection, image upload/serving/deletion, and empty/missing records.
The EF model is compared with the existing migration snapshot without creating or applying migrations.
The checks project is included in hotel.sln, so a solution build compiles it too.

It does not automate visual browser interactions. See hotel/docs/functionality-completion.md for
the manual browser checklist. Run in the normal Windows user context with LocalDB access; restricted
sandbox identities may be unable to open LocalDB. If network restore is unavailable, use existing
restored assets with --no-restore, or restore from the machine's existing NuGet cache. No package
versions need changing. The test app uses a loopback HTTP server and a disposable SQL database.
