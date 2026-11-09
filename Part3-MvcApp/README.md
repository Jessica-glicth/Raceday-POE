# RaceDay Event Management System - Part 3: MVC Web Application

ASP.NET Core 8 **MVC** front end for the RaceDay platform. It talks to the **Part 2 RESTful API** for *all* data
(there is no database access or duplicated business logic in the MVC project), and it is containerised with Docker.
Azure Blob Storage has been added to the **API** for event banners and participant profile pictures.

**YouTube demo (unlisted):** <PASTE LINK HERE>

## CI/CD

![Green build](docs/green-build.png)

> Push to GitHub, open the **Actions** tab, screenshot the green run and save it as `Part3-MvcApp/docs/green-build.png`.
> The workflow builds the API **and the MVC project**, runs the unit tests and builds both Docker images.

## Run with Docker (one command)

Requirements: Docker Desktop. From the `Part3-MvcApp` folder:

```
docker compose up --build
```

| What | URL |
|---|---|
| MVC web app | http://localhost:8081 |
| API (Swagger) | http://localhost:5180/swagger |

This starts SQL Server, an Azurite blob-storage emulator, the API and the MVC app. The first start takes a minute or
two while SQL Server boots (the API waits for it automatically). Stop everything with `Ctrl+C`, then `docker compose down`.

**Run only the MVC container** (when the API is already running on your machine, e.g. from Visual Studio on port 5180):

```
docker build -t raceday-mvc ./RaceDay.Mvc
docker run --rm -p 8081:8080 -e Api__BaseUrl=http://host.docker.internal:5180/ raceday-mvc
```

### Using a real Azure Storage account
The compose file uses Azurite (a local Azure Blob Storage emulator) by default. To use your own Azure Storage account,
set the connection string before `docker compose up`:

```
# PowerShell
$env:AZURE_STORAGE_CONNECTION_STRING = "<your storage account connection string>"
# bash
export AZURE_STORAGE_CONNECTION_STRING="<your storage account connection string>"
```

## Run locally in Visual Studio (without Docker)

1. **Start the API:** open `Part2-RestfulApi/RaceDay.sln`, run `RaceDay.Api` (http profile, port 5180).
   *If you ran Part 2 earlier, delete the old `RaceDayDB` first (SQL Server Object Explorer) so the demo data is re-created
   with upcoming events. The API adds the two new image columns automatically either way.*
2. **Image storage (optional):** without a connection string the app works but image uploads show a friendly
   "storage unavailable" message. To enable uploads, store your Azure Storage connection string with *User Secrets*
   (right-click `RaceDay.Api` -> Manage User Secrets):
   ```json
   { "AzureStorage": { "ConnectionString": "<your storage account connection string>" } }
   ```
3. **Start the MVC app:** open `Part3-MvcApp/RaceDay.Mvc` (or add it to the solution) and run it (http://localhost:5190).
   It expects the API at `http://localhost:5180/` (change `Api:BaseUrl` in `appsettings.json` if needed).

### Demo accounts (password for all: `Password123!`)
| Role | Email |
|---|---|
| Organiser | thabo.nkosi@raceday.co.za |
| Organiser | sarah.vdm@raceday.co.za |
| Participant | lindiwe.dube@example.com |
| Participant | james.botha@example.com |

## How it fits together

* **MVC -> API only.** `ApiClient` is the only way the MVC app gets or changes data.
* **Sessions.** On login the API issues its session cookie. The MVC app keeps that cookie **server-side in its own
  session** (never exposed to the browser) and forwards it on every API call. A separate MVC authentication cookie
  holds the user's id and role, which drives `[Authorize(Roles = ...)]` and the navigation menu. If the API session
  ends, the user is logged out and sent to the login page. Logout clears both.
* **Role-based navigation.** Organisers see Dashboard / My Events / Browse Events. Participants see Events / My Entries /
  My Results. Visitors see Events / Log in / Register. Unauthenticated users are redirected to login; the wrong role
  gets an "Access denied" page.
* **Images through the API.** Uploads go MVC -> API -> Azure Blob Storage. Images are shown through MVC image URLs
  (`/Images/EventBanner/{id}`, `/Images/ProfilePicture`) that fetch from the API, so the MVC app never talks to Azure
  and the blob container stays private.
* **Colour-coded enrolment status:** green = Confirmed, amber = Awaiting Results, blue = Completed.
* **Graceful errors.** Forms validate input, API failures become friendly messages, and unexpected errors show a
  styled error page (never raw exception text).

## Pages

**Public / Participant:** Home (upcoming events + filters by location, type and date), Event details (info, categories,
results, enrol), My Entries (with status and withdraw), My Results (event, date, category, time, position such as
"47th out of 312"), Profile (details + picture), Register, Login.

**Organiser:** Dashboard (events, enrolments per event, upcoming dates), Events management (create / edit / delete with
banner upload), Categories, Enrolments, Results entry, Profile.

## New API endpoints added in Part 3

| Method | Route | Role |
|---|---|---|
| POST / GET / DELETE | /api/events/{id}/banner | Organiser (owner) to upload/delete, public to view |
| POST / GET / DELETE | /api/participants/me/picture | Participant |
| GET | /api/organisers/me/events | Organiser (dashboard data) |
| GET | /api/events?upcoming=true | Public |

Entries now include a `status`, and results include `totalFinishers`, to support the colour coding and "47th of 312".
`Event` gained `BannerBlobName` and `Participant` gained `ProfilePictureBlobName` (nullable) - update your ERD / SQL to match.
