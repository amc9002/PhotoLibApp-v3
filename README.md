# PhotoLib

PhotoLib is an offline-first photo library application: galleries and
photos, browsable and editable even without a server connection, syncing
back once connectivity returns. Frontend in Angular, backend in ASP.NET
Core.

---

## Tech Stack

- **Frontend**: Angular 19 (standalone components), Angular CDK (drag & drop)
- **Backend**: ASP.NET Core 9 Web API
- **Database**: SQLite (development)
- **ORM**: Entity Framework Core
- **Storage**: Local file system (originals + thumbnails, served via dedicated endpoints)
- **Offline cache**: IndexedDB via the `idb` package

---

## Features

### Galleries & photos
- Create, rename, describe, and soft-delete galleries and photos.
- Upload photos from disk (single or multi-select), with EXIF (camera info,
  date, GPS) parsed server-side on upload and shown alongside an embedded
  map in a photo info panel.
- Tags on both photos and galleries, with autocomplete.
- Copy/move photos between galleries; multi-select in the grid
  (Ctrl/Shift-click, drag-marquee) for bulk copy/move/delete.
- Drag-and-drop reordering for both the gallery list and the photo grid.
- **Add from internet**: a generated bookmarklet lets you click any image on
  any web page (including sites with a strict CSP, like Facebook) and send
  it straight into a chosen gallery via a same-origin relay page.
- After a single photo upload (from disk or from the internet), the viewer
  opens with the edit-details panel ready so title/description/tags can be
  filled in immediately.

### Offline support
The app keeps working - reading and writing - while the backend is
unreachable:
- **Local mirror**: gallery and photo lists are cached in IndexedDB and
  served from there when offline or unreachable, so browsing doesn't stop.
- **Bounded image cache**: thumbnails are cached lazily per gallery opened;
  full-size originals are capped at the last 10 distinct photos viewed
  (LRU eviction) to keep the cache small.
- **Outbox**: offline creates, edits, deletes, moves, copies, tag changes,
  and reorders are queued locally (deletes as tombstones, not immediate
  removal) instead of failing.
- **Sync review**: on reconnect, a dialog lists everything that changed
  offline - added / deleted / changed - confirmable individually or in
  bulk. Anything the server itself changed in the meantime shows up as a
  distinct conflict requiring an explicit "apply anyway" instead of being
  silently overwritten.
- Works correctly across page reloads and multiple open tabs (only one tab
  drives sync at a time).

---

## Architecture Notes

- Photo/gallery metadata lives in the database; image files are stored
  separately on disk and served via dedicated endpoints
  (`/api/Photo/{id}/thumbnail`, `/api/Photo/{id}/file`).
- Soft delete (`IsDeleted` + `UpdatedAtUtc`) on both entities; hard-delete
  admin endpoints exist for cleanup in development.
- `SortOrder` on both entities drives manual drag-and-drop ordering.
- `ClientTempId` on both entities makes offline-queued creates idempotent -
  a retried sync replay can't create a duplicate.
- Offline sync's conflict check compares each entity's `UpdatedAtUtc` as
  last known to the client against the server's current value.

---

## Project Structure

```
backend/            ASP.NET Core Web API (controllers, EF Core models/migrations)
frontend/           Angular app
  src/app/core/
    api/             thin HttpClient wrapper
    offline/         IndexedDB mirror, outbox, connectivity, sync coordinator
  src/app/components/  gallery/photo viewer UI
  src/app/services/    PhotoApiService / GalleryApiService (offline-aware)
  src/app/shared/      shared modals, directives, utils
```

---

## Development Setup

### Backend

```bash
cd backend
dotnet restore
dotnet ef database update
dotnet run
```

Runs on `http://localhost:5146` by default (see
`backend/Properties/launchSettings.json`).

### Frontend

```bash
cd frontend
npm install
npm start
```

Runs on `http://localhost:4200`; `proxy.conf.json` forwards `/api` to the
backend during development.
