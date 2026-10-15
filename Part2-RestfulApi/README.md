# RaceDay Event Management System – Part 2: RESTful API

ASP.NET Core 8 Web API + Entity Framework Core (Code-First, SQL Server) with session-based,
role-based authentication, Swagger, xUnit tests and a GitHub Actions CI pipeline.

**YouTube demo (unlisted):** <PASTE LINK HERE>

## CI/CD

![Green build](docs/green-build.png)

> Push to GitHub, open the **Actions** tab, take a screenshot of the green run and save it as
> `Part2-RestfulApi/docs/green-build.png`.

## Setup and run

Requirements: .NET 8 SDK, Visual Studio 2022+ (or VS Code), SQL Server LocalDB / SQL Server Express.

1. Clone the repo and open `Part2-RestfulApi/RaceDay.sln`.
2. Check the connection string in `RaceDay.Api/appsettings.json` (default: LocalDB, database `RaceDayDB`).
   *If you previously ran the Part 1 SQL script, drop that `RaceDayDB` first (or change the database name) –
   EF Core creates the schema itself.*
3. Create the schema with EF Core migrations (Package Manager Console in Visual Studio):
   ```
   Add-Migration InitialCreate -Project RaceDay.Api
   ```
   or from a terminal in `Part2-RestfulApi`:
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate --project RaceDay.Api
   ```
   On startup the app applies migrations automatically and seeds sample data.
4. Run the `RaceDay.Api` project (F5 / `dotnet run --project RaceDay.Api`). Swagger opens at `/swagger`.
5. Run tests: `dotnet test`

### Seeded accounts (password for all: `Password123!`)
| Role | Email |
|---|---|
| Organiser | thabo.nkosi@raceday.co.za |
| Organiser | sarah.vdm@raceday.co.za |
| Participant | lindiwe.dube@example.com |
| Participant | james.botha@example.com |

## The two roles

**Organiser** – manages events and categories they own (create / update / delete), views all enrolments for their
events, captures finish times (finishing positions are calculated automatically per category), manages own profile.

**Participant** – browses events and categories, enrols in a category, views and withdraws own entries,
views own results, manages own profile.

Public (no login): list/view events, view categories, view results, register, login.

## Authentication design

* Registration: the user supplies their details and picks `Organiser` or `Participant`. The account is stored in the
  matching table and **emails are unique across both roles**, so a login always resolves to exactly one role.
* Passwords are hashed with salted PBKDF2-SHA256 (`PasswordService`); the plain text is never stored or returned.
* Login creates a **server-issued session** (encrypted, HttpOnly `RaceDay.Session` cookie) carrying the user id and
  role, so every later request is authenticated and authorised from the session. Logout (`POST /api/auth/logout`) ends it.
* Endpoints use `[Authorize(Roles = ...)]`: no session → **401**, wrong role → **403**. Ownership is also checked
  (an Organiser cannot edit another Organiser's event → **403**).

## Endpoints (from the Part 1 plan)

| Method | Route | Role |
|---|---|---|
| POST | /api/auth/register | Public |
| POST | /api/auth/login | Public |
| POST | /api/auth/logout | Any logged in |
| GET / PUT | /api/organisers/me | Organiser |
| GET / PUT | /api/participants/me | Participant |
| GET | /api/events (filters: date, location, eventType) | Public |
| GET | /api/events/{id} | Public |
| POST | /api/events | Organiser |
| PUT / DELETE | /api/events/{id} | Organiser (owner) |
| GET | /api/events/{eventId}/categories | Public |
| POST | /api/events/{eventId}/categories | Organiser (owner) |
| PUT / DELETE | /api/categories/{id} | Organiser (owner) |
| POST | /api/categories/{id}/entries | Participant |
| GET | /api/participants/me/entries | Participant |
| GET | /api/events/{id}/entries | Organiser (owner) |
| DELETE | /api/entries/{id} | Participant (own) or Organiser (owner) |
| POST | /api/entries/{id}/result | Organiser (owner) |
| GET | /api/events/{id}/results | Public |
| GET | /api/categories/{id}/results | Public |
| GET | /api/participants/me/results | Participant |

## Changes made to the Part 1 design (update your ERD / SQL to match)

1. **Organiser.PasswordHash** added – the Part 1 seed script already inserts it, but its `CREATE TABLE` omitted it.
2. **Event** gained `Description`, `Distance` and `EventType` (Run/Walk/Cycle) – required by the Part 2 brief.
3. **Result** gained `Position` (finishing position) – required by the Part 2 brief.
4. The plan listed `PUT /api/participants/me` twice; the Organiser one is implemented as `PUT /api/organisers/me`.
5. Authentication is a session cookie rather than the JWT in the Part 1 table (the Part 2 brief asks for sessions).
6. Request bodies use `firstName / lastName / contactNumber` to match the database columns.
