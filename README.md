# 🏨 Hotel Management System

A full-stack Hotel Management System built with **ASP.NET Core MVC**, **Entity Framework Core**, **SQL Server**, and **ASP.NET Identity**.

The system provides separate experiences for **Customers** and **Administrators**, including room management, online booking, seasonal pricing, offers, inventory tracking, contact management, and role-based access control.

---

## ✨ Features

### 👤 Customer Features

- Register and login using ASP.NET Identity
- Browse available hotel rooms
- View room details, images, and services
- Select check-in and check-out dates
- Automatic booking price calculation
- Seasonal price adjustments
- Long-stay offer discounts
- Booking conflict detection
- Submit booking requests
- View personal booking history
- Track booking status:
  - Pending
  - Approved
  - Rejected
  - Completed
- View rejection reason when applicable
- Contact the hotel through the Contact Us page

### 🛠️ Admin Features

- Admin dashboard
- Room management
- Room image management
- Room service management
- Booking management
- Approve booking requests
- Reject bookings with a rejection reason
- Complete bookings / checkout
- Booking status filtering
- Seasonal pricing management
- Offers management
- Inventory management
- Low-stock monitoring
- Customer message inbox
- Mark messages as read
- Role-based access control

---

## 💰 Booking & Pricing System

Booking prices are calculated on the server to prevent client-side manipulation.

The pricing system supports:

- Base room price per night
- Number of booked nights
- Seasonal price multipliers
- Applicable offer discounts
- Server-side total recalculation

The system also checks room availability before creating and approving bookings.

Overlapping **Pending** and **Approved** bookings are handled according to the booking availability rules, while adjacent stays are supported.

---

## 🔐 Authentication & Authorization

Authentication is implemented using **ASP.NET Core Identity**.

The system supports two roles:

- `Admin`
- `Customer`

New users are automatically assigned the `Customer` role.

Administrative functionality is protected using role-based authorization.

Sensitive booking information such as:

- User ID
- Total amount
- Booking status

is determined or validated on the server.

---

## 🧰 Technologies Used

- C#
- .NET 8
- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- Razor Views
- Bootstrap 5
- JavaScript
- HTML5
- CSS3
- Git
- GitHub

---

## 🗃️ Main Entities

The system includes the following main entities:

- ApplicationUser
- Room
- RoomImage
- RoomService
- Booking
- SeasonPricing
- Offer
- Inventory
- ContactMessage

---

## 🏗️ Project Structure

```text
hotel/
│
├── Controllers/
├── Data/
├── Models/
├── Services/
├── ViewModels/
├── Views/
├── wwwroot/
│   ├── css/
│   ├── images/
│   └── js/
├── Migrations/
└── Program.cs

tests/
└── CustomerBookingChecks/
```

---

## 🛡️ Security

The project includes several security measures:

- ASP.NET Core Identity authentication
- Role-based authorization
- Anti-forgery protection
- Server-side validation
- Booking ownership validation
- Server-side price calculation
- Booking overlap validation
- Safe ReturnUrl handling
- Protection against overposting
- Admin-only management endpoints

---

## 🧪 Testing

The project includes automated regression checks covering important functionality such as:

- Authentication and authorization
- Customer booking ownership
- Booking conflicts
- Adjacent bookings
- Booking lifecycle
- Pricing calculations
- Seasonal pricing
- Offers
- Inventory validation
- Contact messages
- Room history protection

The final regression suite completed with:

```text
227 checks passed
0 build errors
0 build warnings
```

---

## 🚀 Getting Started

### Prerequisites

Install:

- .NET 8 SDK
- SQL Server / SQL Server LocalDB
- Visual Studio 2022

### 1. Clone the repository

```bash
git clone https://github.com/abonaeem657/Hotel-Management-System.git
```

### 2. Open the project

Open:

```text
hotel.sln
```

in Visual Studio.

### 3. Configure the database

The project uses Entity Framework Core with SQL Server.

Update the connection string if necessary, then apply the existing migrations:

```bash
dotnet ef database update --project hotel
```

### 4. Configure the Development Admin

The project supports development Admin seeding through .NET User Secrets.

From the application project directory:

```bash
dotnet user-secrets set "DevelopmentAdmin:Email" "YOUR_ADMIN_EMAIL"
dotnet user-secrets set "DevelopmentAdmin:Password" "YOUR_STRONG_ADMIN_PASSWORD"
```

Do not store real passwords directly in source control.

### 5. Run the application

```bash
dotnet run --project hotel
```

Or run the project directly from Visual Studio.

---

## 📸 Screenshots

Screenshots of the application can be added here.

Recommended screenshots:

- Home Page
- Customer Rooms
- Room Details & Booking
- My Bookings
- Login / Register
- Admin Dashboard
- Room Management
- Booking Management
- Inventory Management

---

## 🔮 Possible Future Improvements

Potential future enhancements include:

- Email verification
- Password recovery
- Two-factor authentication
- Online payment integration
- Email booking notifications
- Advanced reporting
- Deployment to a cloud hosting platform

---

## 👨‍💻 Author

**Mohammed Al-Shreef**

Computer Science Student

GitHub: [abonaeem657](https://github.com/abonaeem657)

---

## 📄 License

This project was developed for educational and portfolio purposes.
