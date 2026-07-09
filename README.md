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
- **Containerization**: Docker Compose (see Quick Start below)

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

### AI-assisted descriptions (optional)
- The photo edit-details modal has a "Generate with AI" section: pick a
  writing style (artistic / informative / scientific / journalistic) and
  length, optionally add free-form instructions (e.g. a target language or
  facts you already know), and Claude drafts a title, description and tags
  for the photo - using web search to identify the actual subject and
  ground the text in real facts rather than just describing pixels. Nothing
  is saved automatically; the draft lands in the form for you to edit and
  save (or discard) like any other change.
- Requires your own Anthropic API key - see **Development Setup** below.
  Without one configured, the button fails with an inline error instead of
  crashing; every other feature in the app works normally without it.

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

## Quick Start (Docker)

> Not familiar with git/Docker/terminals at all? See
> [`INSTALL-SIMPLE.md`](INSTALL-SIMPLE.md) for a no-jargon, click-by-click
> walkthrough instead.

The fastest way to run PhotoLib without installing .NET, Node, or any
other tooling - just [Docker](https://www.docker.com/products/docker-desktop/):

```bash
git clone <this repo>
cd photolib-ng-v3
docker compose up -d
```

Open **http://localhost:4200**. One command brings up three containers -
the ASP.NET Core backend, nginx serving the built Angular app and
proxying `/api/*` requests to the backend, and a backup service (see
**Backups** below) - plus a persistent named Docker volume for the
SQLite database and uploaded photos, so your data survives
`docker compose down` and container rebuilds. Database migrations run
automatically on startup, so there's no separate setup step.

To enable the optional **AI-assisted descriptions** feature, copy
`.env.example` to `.env` and set `ANTHROPIC_API_KEY` *before* running
`docker compose up` (see that section below for how to get a key).
Everything else works normally without it. If you're hosting a single
shared instance for others rather than everyone running their own
container, note that the AI endpoint has no per-user auth or quota yet -
just a per-IP rate limit - so consider setting `AI_ENABLED=false` in
`.env` until that exists (see `TODO,md`).

```bash
docker compose down        # stop, keep your data
docker compose down -v     # stop and wipe the database/photos volume too
```

This is the recommended path if you just want to run the app. For active
development (hot reload, debugging, editing code) use the manual setup
below instead.

### Backups

The `backup` container takes a consistent snapshot of the database and
uploaded photos every 24 hours by default (configurable via
`BACKUP_INTERVAL_HOURS`/`BACKUP_KEEP` in `.env`, default: keep the last
14 of each) and writes them to `./backups` **on the host**, not just
inside a Docker volume - so a bad migration, an accidental delete, or a
corrupted volume doesn't mean total data loss.

This is a *local* safety net, not a full disaster-recovery plan: if the
machine's disk fails, `./backups` is gone too. Periodically copy that
folder somewhere else (external drive, cloud storage) for real
protection.

To restore from a backup (stop the app first):

```bash
docker compose down
gunzip -c backups/photolib-db-<timestamp>.db.gz > /path/to/restored/photolib.db
tar -xzf backups/photolib-uploads-<timestamp>.tar.gz -C /path/to/restored
# copy restored/photolib.db and restored/uploads into the photolib-data
# volume (e.g. via a temporary container), then:
docker compose up -d
```

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

Manual setup, for working on the code (hot reload, debugging, EF
migrations). If you just want to run the app, see **Quick Start (Docker)**
above instead.

### Backend

```bash
cd backend
dotnet restore
dotnet ef database update
dotnet run
```

Runs on `http://localhost:5146` by default (see
`backend/Properties/launchSettings.json`).

#### AI-assisted descriptions (optional)

The "Generate with AI" feature calls the Anthropic API and needs its own
API key - separate from any Claude subscription, billed pay-as-you-go on
[console.anthropic.com](https://console.anthropic.com) (a new account
usually needs a minimum credit purchase, e.g. $5, before the key works;
each generation costs a few cents). Configure it via .NET user-secrets so
it never ends up in a committed file:

```bash
cd backend
dotnet user-secrets init
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
```

Skip this if you don't want the feature - everything else works fine
without it.

### Frontend

```bash
cd frontend
npm install
npm start
```

Runs on `http://localhost:4200`; `proxy.conf.json` forwards `/api` to the
backend during development.
