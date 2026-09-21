# Customer room browsing and booking

Public pages: `/CustomerRooms`, `/CustomerRooms/Details/{id}`. Protected pages/actions:
`/Bookings/MyBookings` and POST `/Bookings/Create`. `/Rooms` remains the existing management UI.

## Availability and dates

- `Room.IsAvailable` enables a room generally. Date availability is checked separately.
- Pending and Approved bookings block overlapping stays; Rejected and Completed do not.
- Stays use `[check-in, check-out)`: another guest may arrive on the previous guest's checkout date.
- Dates are normalized to midnight. Past-date validation uses the web server's local calendar date.
- Creation repeats validation and availability checks in a SQL Server serializable transaction, taking
  an update/hold lock on the room before checking overlap and inserting. Concurrent customer requests
  for that room serialize. The schema and booking status enum are unchanged.
- Pending requests hold the dates until their status changes. There is no automatic expiry.

## Pricing

- Base subtotal is nights times the current room base rate.
- Seasonal pricing applies separately to each occupied night. Season start/end dates are inclusive;
  checkout is not an occupied night. Outside seasons the multiplier is 1. When seasons overlap,
  the highest configured multiplier wins (multipliers below 1 are supported).
- The highest valid discount among offers with `MinimumNights <= stay nights` is applied once to the
  seasonal subtotal. Offers do not stack. Existing offers are global because the model has no room,
  activation, or date fields. Invalid season ranges/multipliers and invalid offers are ignored.
- Subtotals and discounts are rounded to two decimals, midpoint away from zero.
- The price preview is calculated on the server. Submission always calculates again using current
  database prices; no browser-supplied user ID, amount, status, or rejection reason is bound.
- The project has no currency setting. Amounts are displayed without inventing a currency symbol.

## Identity

ASP.NET Identity and `ApplicationUser` are reused. Booking ownership comes from `UserManager` and the
authenticated principal; My Bookings filters by that ID in SQL and exposes no arbitrary booking ID route.

Login/Register and POST Logout use the existing Identity setup. Public guests can browse and calculate
a price; the booking panel's Login link preserves the room and dates. After login, calculate again
and confirm. Normal registration assigns Customer. Admin management requires the Admin role.
See [authentication.md](authentication.md) for role seeding and development Admin configuration.
No migration is required by this customer flow.

## Manual checks

Browse Home and Rooms on desktop/mobile; confirm the catalog has no filters and check empty results, placeholders,
gallery arrows and thumbnails. Calculate a stay spanning season boundaries and qualifying offers.
Sign in as two customers, confirm a request, verify Pending status
and ownership isolation, and attempt overlapping and adjacent stays. Rejected booking reasons should
appear only for their owner. Check all existing `/Rooms` modals, uploads, services, and deletion.

## Admin lifecycle

`/Bookings` is Admin-only, with optional `?status=Pending`, `Approved`, `Rejected` or `Completed`.
Pending can become Approved or Rejected; Approved can become Completed. Rejection requires a
nonblank reason. Approval rechecks approved overlaps. All transitions acquire the same SQL Server
room lock used by customer creation, then reload the booking status inside the transaction.
No transition changes Room.IsAvailable. Pending bookings continue to hold dates during customer
creation, as documented above. Checkout is an explicit Admin action; it does not require the scheduled
checkout date to have arrived, allowing early checkout. Saved booking prices are not recalculated by
status changes or later pricing edits. Rooms with any booking history cannot be deleted; disable them
instead. The database schema and its existing cascade relationship remain unchanged.

Season multipliers and offer percentages accept at most two decimal places, matching the existing
decimal(18,2) columns. A positive multiplier smaller than 0.01 cannot be represented by this schema.
Unrepresentable or overflowing stay totals produce a validation message instead of a server error.
