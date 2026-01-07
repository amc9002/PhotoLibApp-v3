# PhotoLib

PhotoLib is an offline-first photo library application.
The project is built with Angular on the frontend and .NET Web API on the backend.

This repository is under active development and evolves step by step.

---

## Tech Stack

- Frontend: Angular
- Backend: ASP.NET Core Web API
- Database: SQLite (development)
- ORM: Entity Framework Core
- Storage: Local file system (originals + thumbnails)

---

## Architecture Notes

- Photo metadata is stored in the database
- Image files are stored separately on disk
- Thumbnails and original images are accessed via dedicated API endpoints
- The project follows an offline-first approach
- Photos use two identifiers:
  - client-side temporary ID
  - server-side GUID

---

## Development Setup

### Backend

```bash
cd backend
dotnet restore
dotnet ef database update
dotnet run
```
