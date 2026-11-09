# Salė+

Salė+ is a web application for finding exercises, creating workouts, and sharing fitness-related experiences. Users can browse exercises and workouts, explore exercises by muscle group, and interact with other users.

## Features

- Browse and search exercises.
- Explore exercises by muscle group.
- View workouts and the exercises they contain.
- Create and manage exercises.
- Create and manage workouts.
- Comment on exercises and workouts.
- Rate exercises and workouts.
- Save exercises and workouts.
- Report inappropriate content.
- Administrator functionality for managing users, exercises, workouts, comments, and reports.

## Technologies

- **Frontend:** Blazor WebAssembly, C#
- **Backend:** ASP.NET Core Web API, C#
- **Database:** Firebase Cloud Firestore
- **API architecture:** REST

## Project Structure

```text
SalePlus/
├── SalePlus.Client/    # Blazor frontend
├── SalePlus.Api/       # ASP.NET Core Web API
├── SalePlus.Shared/    # Shared models and DTOs
└── SalePlus.sln
```

## Prerequisites

To run the project locally, you need:

- Visual Studio 2022 with the ASP.NET and web development workload.
- A compatible .NET SDK for the project.
- A Firebase project with Firestore Database enabled.
- Firebase service account credentials for backend access.

## Configuration

### Firebase

1. Create a Firebase project or use an existing one.
2. Enable Firestore Database and set its security rules to deny all direct access
   (`allow read, write: if false;`). The API uses the service account, which is not affected by these rules.
3. Download a service account key (JSON) and keep it **outside** the repository.

The project ID is in `SalePlus.Api/appsettings.json` (`Firebase:ProjectId`).

### Secrets

Secrets are never committed. On a development machine they are kept with .NET user-secrets
(run in the `SalePlius/SalePlus.Api` folder):

```bash
dotnet user-secrets set "Jwt:Key" "<at least 64 random characters>"
dotnet user-secrets set "Firebase:CredentialsPath" "C:\path\to\firebase-adminsdk.json"
```

When hosted, the same settings come from environment variables:

| Variable | Meaning |
|---|---|
| `Jwt__Key` | Key that signs access tokens |
| `Firebase__CredentialsJson` | Contents of the service account JSON (or `Firebase__CredentialsPath` for a file) |
| `Seed__AdminPassword` | Password of the first admin, only used when the database is empty |
| `Cors__AllowedOrigins__0` | Address of the hosted client |

### HTTPS development certificate

If the API fails with "the default developer certificate could not be found or is out of date", renew it:

```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```


## Running the Project

1. Clone the repository:

   ```bash
   git clone YOUR_REPOSITORY_URL
   ```

2. Open `SalePlus.sln` in Visual Studio.

3. Set the secrets described in [Configuration](#secrets).

4. Run the API first then when it's ready go for the client.

## Authors

Faustas Ringaitis
