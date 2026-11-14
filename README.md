# PhotoLib

PhotoLib is a self-hosted, offline-first photo library for a small group of
people (a household or a few friends) sharing one instance over a private
network (e.g. Tailscale) — each person signs in to their own account with
their own private galleries. Frontend in Angular, backend in ASP.NET Core.

It is not currently a public/multi-tenant cloud product and assumes no
exposure to the open internet. A plan to evolve it into a hybrid
Local-first Desktop + optional Cloud/Social platform is documented
separately in [`ARCHITECTURE-PROPOSED.md`](ARCHITECTURE-PROPOSED.md) and
[`MIGRATION-PLAN.md`](MIGRATION-PLAN.md) — none of that is implemented yet;
everything below describes what exists today.

---

## Tech Stack

- **Frontend**: Angular 19 (standalone components), Angular CDK (drag &
  drop, virtual scroll), `@angular/animations`
- **Backend**: ASP.NET Core 9 Web API
- **Database**: SQLite (same engine in development and in the Docker image
  — there is no separate production-grade database yet)
- **ORM**: Entity Framework Core
- **Storage**: Local file system (originals + thumbnails + avatars, served
  via dedicated endpoints)
- **Offline cache**: IndexedDB via the `idb` package
- **Auth**: cookie-based sessions, per-user accounts
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

### Accounts & profile
- Each person signs in with their own email/password; accounts are
  created either by self-service registration (open on the instance, safe
  because reachability itself is gated by your private network) or via an
  admin-key-gated endpoint for the instance owner's own first account -
  see [`FRIENDS-ACCESS-GUIDE.md`](FRIENDS-ACCESS-GUIDE.md) for the full
  walkthrough to send to friends.
- Every gallery and photo is private to its owner; nothing is shared
  between accounts on the same instance today.
- Edit your display name and bio, and upload an avatar with a client-side
  pan/zoom crop step before it's sent to the server.
- There is no password-reset flow - losing a password means losing access
  to that account's library, with no self-service recovery. Keep this in
  mind before sharing an instance with people you can't easily walk through
  re-registering.

### Discovery feed
- A default landing view (shown when no gallery is selected) that shows an
  endless, shuffled grid of your own photos pulled from across all your
  galleries, rather than requiring you to pick a gallery first.
- Quietly learns which photo tags you actually open in the full-screen
  viewer and nudges later batches toward more of that - capped, so unrelated
  photos always keep showing up too, never narrowing to just one theme.
- Scoped to your own photos only for now; there's no cross-user or public
  content model yet (see `ARCHITECTURE-PROPOSED.md` for where that's headed).
- Collapses to a narrow strip on the right edge (rather than disappearing)
  when you select a gallery, so it's always one click away via the toolbar.

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
- Every gallery (and transitively every photo in it) belongs to exactly one
  user; ownership checks are centralized in a single service, and a failed
  check always returns 404 (never 403), so a guessed id for someone else's
  content is indistinguishable from one that doesn't exist.
- Soft delete (`IsDeleted` + `UpdatedAtUtc`) on both entities; hard-delete
  admin endpoints exist for cleanup in development.
- `SortOrder` on both entities drives manual drag-and-drop ordering.
- `ClientTempId` on both entities makes offline-queued creates idempotent -
  a retried sync replay can't create a duplicate.
- Offline sync's conflict check compares each entity's `UpdatedAtUtc` as
  last known to the client against the server's current value.
- View swaps, modals, dropdowns and context menus ease in/out with a short
  shared animation (`@angular/animations`) instead of appearing instantly.

See [`PROJECT-ANALYSIS.md`](PROJECT-ANALYSIS.md) for a full technical
breakdown (data model, API surface, offline-sync internals, known
limitations) verified directly against the code.

---

## Project Structure

```
backend/            ASP.NET Core Web API (controllers, EF Core models/migrations)
frontend/           Angular app
  src/app/core/
    api/             thin HttpClient wrapper (every API call goes through this)
    auth/            session state, 401-handling interceptor
    offline/         IndexedDB mirror, outbox, connectivity, sync coordinator
    i18n/            BE/EN translations, language switching
    theme/           background theme presets
  src/app/components/  gallery/photo viewer UI, login, discovery feed
  src/app/services/    PhotoApiService / GalleryApiService (offline-aware),
                       DiscoveryApiService / TagApiService (online-only)
  src/app/shared/      shared modals, directives, animations, utils
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

To create your first (owner) account in dev mode, set an admin key via
user-secrets and call the admin-registration endpoint with it, the same way
the Docker setup's `AUTH_ADMIN_KEY` works:

```bash
dotnet user-secrets set "Auth:AdminKey" "some-long-random-value"
# then POST /api/Auth/admin/register with header X-Admin-Key: some-long-random-value
# (e.g. via Swagger at http://localhost:5146/swagger, or curl)
```

Anyone else can self-register afterward through the normal login screen's
"create account" option - only the admin-key path claims any pre-existing
data and is meant for the instance owner's own first account.

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
