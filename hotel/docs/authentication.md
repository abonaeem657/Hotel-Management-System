# Identity authentication

The existing ApplicationUser, IdentityRole, Identity tables, UserManager and SignInManager are used.
There are no schema changes or migrations. Password and lockout defaults are preserved; email must
be unique. Login uses Identity lockout-on-failure and a generic failure message. All account POSTs
require anti-forgery tokens. Logout is POST only.

## Roles and access

At startup, IdentitySeeder ensures Admin and Customer roles exist using RoleManager. Existing roles
are reused. Register accepts only profile/password fields and always assigns Customer in the same
database transaction as account creation, before signing in. Browser-supplied roles are ignored.
Registration redirects to CustomerRooms. Existing accounts are not silently assigned or elevated.

RoomsController, RoomServicesController, AdminController, SeasonPricingController, OffersController,
InventoryController and InboxController require Admin for every action. Bookings/Index and lifecycle
POSTs require Admin; Bookings/Create and MyBookings require Customer. MyBookings retains its
server-side ownership filter. CustomerRooms and Contact remain public. Navigation follows roles.

## Development Admin configuration

The Admin seed runs only when the hosting environment is Development. Both keys are required:

- `DevelopmentAdmin:Email`
- `DevelopmentAdmin:Password`

The project has a UserSecretsId. From the project directory, configure the keys with `dotnet
user-secrets set` or Visual Studio's Manage User Secrets. Supply your own email and strong password;
never put credentials in committed appsettings files. Environment variable equivalents are
`DevelopmentAdmin__Email` and `DevelopmentAdmin__Password`. Start with ASPNETCORE_ENVIRONMENT set
to Development (or use your Development launch profile).

Missing credentials skip account creation without stopping startup. Invalid credentials also skip
creation with a diagnostic containing Identity error codes, never the password. Roles are still seeded.
The seed does not reset existing passwords or promote an existing Customer account with the same
email. Configure an unused development email if there is such a collision. Repeated startup does not
duplicate the seeded account. Production never seeds an Admin account, even if these keys are set.

No development credentials are included or configured by this change. Production Admin provisioning
must be done through your deployment's trusted Identity administration process.

## Login and booking return flow

Login redirects only to a URL accepted by Url.IsLocalUrl. Without a local ReturnUrl, Admin goes to
the Admin dashboard and other users go to CustomerRooms. Access-denied challenges go to Account/AccessDenied.
The booking panel links to Login with the room and selected dates as its local return URL. After
login, calculate the price again and confirm. Booking creation is never replayed automatically.
Admin modal requests that encounter an expired session navigate to Login rather than treating the
authentication redirect as a successful save.

## Manual checks and remaining work

Test `/Account/Register`, `/Account/Login`, `/CustomerRooms`, `/CustomerRooms/Details/{id}` and
`/Bookings/MyBookings`. Logout uses the navigation form. Test `/Rooms` and `/RoomServices` as guest,
Customer and Admin; only Admin should access them. AccessDenied returns HTTP 403.

Requested registration/login/logout functionality is implemented. Email verification, password
recovery, two-factor UI and profile editing are not included in this task. If enabling confirmed
email or two-factor requirements later, implement their corresponding flows before enabling them.
