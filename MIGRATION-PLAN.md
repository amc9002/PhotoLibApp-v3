# PhotoLib — Migration Plan

> **Status: FUTURE / PROPOSED.** This is a plan, not a changelog — none of
> the phases below have started. It describes how to get from today's
> codebase (`PROJECT-ANALYSIS.md`) to the target architecture
> (`ARCHITECTURE-PROPOSED.md`) without breaking what currently works or
> losing user data along the way.

---

## 0. How to read this document

Each phase lists:
- **Goal** — what becomes true once the phase is done.
- **Required changes** — concretely, what gets built or modified.
- **Dependencies** — which earlier phases (or existing `TODO,md` items)
  must already be done.
- **Risks** — what could go wrong, and how serious.
- **Data migration** — whether existing users' data needs a transformation,
  and what happens to it if the phase is abandoned partway.
- **Acceptance criteria** — how to know the phase is actually complete,
  not just "mostly working."

Phases are **sequential by dependency, not by calendar** — a phase can sit
unstarted indefinitely without blocking the current app from working;
nothing here is on a deadline, and the current single-backend app keeps
working throughout every phase until Phase 6 deliberately changes its
behavior.

**Relationship to existing `TODO,md` phases**: `TODO,md`'s SaaS/
Monetization section already has its own Phase 1 (auth — **done**, see
`PROJECT-ANALYSIS.md`), Phase 2 (AI quotas), Phase 3 (billing), Phase 4
(security hardening), Phase 5 (PostgreSQL + monitoring). Those are
orthogonal to most of this plan except where explicitly cross-referenced
below — e.g. this plan's Phase 4B (independent cloud storage, full scale)
has a hard dependency on `TODO,md`'s Phase 5 (PostgreSQL), not because
cloud storage needs billing, but because SQLite's single-writer model
(already flagged as a known limitation) cannot safely serve a real
multi-tenant cloud database. (Phase 4A, the small-scale pilot described
below, deliberately stays on SQLite and does **not** depend on `TODO,md`
Phase 5 — see the owner-decisions note after the diagram.)

**Two tracks, not one sequential list**: Phases 1–3 below ("Track A") take
the app from today's single shared backend to a standalone Desktop
application. Phases 4–7 ("Track B") take it from there to an optional
Cloud/Social layer. Per the project owner's decision of 2026-11-15, **Track
A is the current priority and Track B is on hold** until the Desktop MVP
has been built and proven reliable — the phase descriptions below still
document Track B in full so the plan doesn't need to be rewritten when
work resumes on it, but no Track B phase is active work right now.

```mermaid
graph TD
    subgraph TrackA["Track A — Desktop (ACTIVE)"]
        P0["Phase 0\nDocument current architecture\n(DONE)"]
        P1["Phase 1\nSeparate local data access\nfrom cloud API dependencies"]
        P2["Phase 2\nGenuine local-first storage model\n+ local library export"]
        P3["Phase 3\nDesktop packaging"]
        P0 --> P1 --> P2 --> P3
    end

    subgraph TrackB["Track B — Cloud/Social (ON HOLD until Track A validated)"]
        P4A["Phase 4A\nSocial pilot on EXISTING\ninfrastructure (3-5 -> 10-20 users)"]
        P4B["Phase 4B\nIndependent cloud storage,\nfull scale (PostgreSQL + object store)"]
        P5["Phase 5\nSelective sync & publishing"]
        P6["Phase 6\nSocial functionality"]
        P7["Phase 7\nBrowser-based cloud\ninterface (Web)"]
        P4A -.->|"pilot validates demand/stability"| P4B
        P4B --> P5 --> P6 --> P7
    end

    P1 -.->|"not started yet;\nTrack A must land first"| P4A
    P1 -.-> P4B
    P2 -.-> P5
    P3 -.-> P5

    TODOp5["TODO,md Phase 5\n(PostgreSQL migration)"] -.->|"hard dependency"| P4B
    TODOp2["TODO,md Phase 2\n(AI usage quotas)"] -.->|"should land before\nmulti-tenant cloud traffic"| P4B

    style P4A stroke-dasharray: 5 5
    style P4B stroke-dasharray: 5 5
    style P5 stroke-dasharray: 5 5
    style P6 stroke-dasharray: 5 5
    style P7 stroke-dasharray: 5 5
```

---

## Owner decisions (2026-11-15)

Three sequencing/scoping questions were resolved directly by the project
owner, and this revision of the plan incorporates them:

1. **Track A first.** Track B's full parallel build does not start now.
   The immediate priority is a working, reliable Desktop MVP (Phases 1–3).
   However, Track A's design must already account for Track B's known
   future needs, so Desktop's local architecture doesn't need reworking
   later purely to accommodate the Cloud/Social layer — see "Forward-looking
   architectural decisions" below, placed right after Phase 1.
2. **Export format.** The first implementation of local library export
   (Phase 2) uses ZIP + JSON: untouched original files plus a JSON
   manifest of gallery structure and metadata. It must be **versioned and
   documented** from day one so it can be extended later without breaking
   older exports — XMP sidecar export is a plausible future addition,
   explicitly deferred rather than built now, to avoid complicating the
   first implementation without a proven need.
3. **Pilot sizing and availability.** When Track B resumes, its first
   social pilot (Phase 4A) targets 3–5 participants, scaling to 10–20 if
   stable. Running on the existing shared Tailscale instance means the
   pilot is unavailable whenever that host or its network is down — the
   owner has explicitly accepted this as **a limitation of the test
   environment, not a flaw in the target architecture**, so Phase 4A's
   risk assessment below is scoped accordingly (it would be a real problem
   at public/investor-demo scale, which is exactly what Phase 4B exists to
   solve once the pilot has validated the feature set).

**Three further clarifications (2026-11-15, second round), all applied
below before Phase 1 begins:**

4. **Desktop auto-update deferred to public release.** Phase 3's pilot
   (trusted testers only) ships with manual updates; the auto-update
   mechanism becomes a public-release requirement, not a pilot one.
5. **Installer signing deferred to public release.** Phase 3's pilot does
   not require a signed installer. A distribution-and-verification method
   for the Windows installer must be defined first (e.g. a checksum
   published alongside the download); code-signing/notarization
   requirements are evaluated before the public release, not before the
   pilot.
6. **Local-mode API access security needs its own analysis.** Binding the
   no-auth local backend to `127.0.0.1` (Phase 2) is necessary but not
   sufficient — anything running in the user's browser on that machine
   (a malicious web page, a browser extension) can still reach
   `localhost`. Phase 2 must explicitly analyze and mitigate this (e.g.
   CORS restricted to the app's own origin, `SameSite` cookies if any
   session concept remains, or a per-launch local token the Tauri shell
   injects) rather than relying on the loopback bind alone.

## Phase 0 — Document current architecture *(done)*

**Goal**: an accurate, code-verified description of what exists today, so
every later phase has a trustworthy baseline instead of working from
assumptions or stale docs.

**Status**: complete as of this session. `PROJECT-ANALYSIS.md` rewritten
from direct code inspection; `README.md` and `TODO,md` reconciled;
`ARCHITECTURE-PROPOSED.md` and this document added.

**Acceptance criteria**: ✅ met — `PROJECT-ANALYSIS.md` distinguishes
implemented vs. planned functionality throughout, verified against actual
files rather than prior documentation.

---

## Phase 1 — Separate local data access from cloud API dependencies

**Goal**: the frontend's data-access layer has one clean seam between
"talks to a backend over HTTP" and "everything else," so a later phase can
swap *which* backend (local vs. cloud) without touching UI components.

**Required changes**:
- Audit confirms this is **mostly already true** (`PROJECT-ANALYSIS.md`
  §11) — every network call funnels through `ApiService`. The gap is
  narrower than a full refactor: formalize it as an explicit, documented
  contract (an interface or equivalent convention) rather than an
  accidental property, so it doesn't silently erode as new features are
  added (the way a new feature *could* reach for `HttpClient` directly if
  no one's enforcing the convention).
- Introduce a lightweight **data-source selection point** (e.g. a single
  injected base-URL/config token) so "local backend at `localhost:{port}`"
  vs. "cloud backend at `api.photolib.example`" is a configuration
  difference, not a code difference, in every service under `services/`.
- No change needed to `core/offline/` — its "the backend might be
  unreachable" assumption already fits a local-first world unchanged.

**Dependencies**: none (can start immediately).

**Risks**: low. This is mostly a discipline/documentation exercise given
how clean the existing boundary already is — the main risk is scope creep
into "let's also refactor X while we're in here," which should be resisted
to keep this phase low-risk and fast.

**Data migration**: none — no schema or stored-data changes.

**Acceptance criteria**:
- A documented rule (e.g. a lint rule or code-review checklist item) that
  no component/feature service injects `HttpClient` directly.
- The base API URL is read from one injectable config point, not
  hardcoded as `/api/...` relative paths scattered through service files.
- Existing app behavior is 100% unchanged — this phase is invisible to
  users.

---

## Forward-looking architectural decisions (apply during Track A, so Track B doesn't require rework)

Per owner decision 1 above: Track B is not being built now, but a short
list of decisions, applied while building Track A, avoids having to
re-architect Desktop's local storage later. This list is deliberately
short — most of the Cloud/Social model (visibility levels, Friendship,
Subscriptions, Activity Feed) genuinely has **no bearing on Track A** and
is not pre-built here, since Desktop's local `Gallery`/`Photo` tables have
no concept of other users at all until a cloud account exists.

- **Keep the mutation outbox structurally single-purpose.** Today's
  `OutboxEntry`/`SyncCoordinatorService` (`PROJECT-ANALYSIS.md` §8.2–8.3)
  queues edits to content *you own*. Track B will eventually need a
  separate, read-only concept for content you don't own but can see
  (another user's shared photo, per `ARCHITECTURE-PROPOSED.md` §6.1's
  domain 3/4 distinction). Track A doesn't need to build that second
  concept now — it just needs to **not** grow the outbox into a
  general-purpose "cache of arbitrary remote objects," which would make
  that future separation harder to retrofit.
- **Treat local ids as locally-stable, not globally-stable.** Nothing
  changes in Phase 1/2 — `ClientTempId`-style local ids keep working
  exactly as today. The only discipline is: don't start treating a local
  id as if it were (or will become) a cross-device/cross-account id. When
  Track B eventually assigns a cloud id at publish time
  (`ARCHITECTURE-PROPOSED.md` §3.3), it will be a new, separate value, not
  a reinterpretation of the local one.
- **Keep the local export format (Phase 2) genuinely self-describing.**
  Not because it will be reused unchanged as a future publish/sync
  payload (that's not decided), but because a versioned, documented
  export format is cheap now and expensive to retrofit onto data that was
  never designed to leave the device.
- **No new persisted state for Discovery, visibility, or social concepts.**
  `DiscoveryFeedService`'s existing `PhotoView`-based weighting
  (`PROJECT-ANALYSIS.md`) is already confirmed reusable unchanged when
  Track B extends its candidate pool later — there is nothing to
  pre-build here, and adding speculative fields (a `Visibility` column, a
  friend-list placeholder, etc.) to Track A's schema now would violate
  the standing "don't add object/feature types without justification"
  constraint for no present benefit.

---

## Phase 2 — Introduce a genuine local-first storage model

**Goal**: the *backend itself* can run fully standalone, pointed at a
local SQLite file and local photo folder, with no cloud account, no
Tailscale/network assumption, and no cookie-auth requirement for a
single-user local install.

**Required changes**:
- A **local-mode configuration profile** for the existing ASP.NET Core
  backend: `Storage:PhotosPath`/`Storage:AvatarsPath` pointed at a
  user-chosen local folder (not a Docker volume path), SQLite connection
  string pointed at a local file (e.g. inside the OS's standard app-data
  directory), bound to `127.0.0.1` only.
- A **local-mode auth bypass**: single-user local installs shouldn't
  require sign-in/cookie sessions at all (per `ARCHITECTURE-PROPOSED.md`
  §2.A's "no authentication required"). This needs a deliberate decision
  about how `[Authorize]`/`GalleryAccessService`'s ownership model behaves
  with no signed-in user — likely a synthetic "local owner" identity
  created on first run, so the existing ownership-scoped query shape
  doesn't need to fork into two code paths.
- **A dedicated access-security analysis for the no-auth local API** — per
  owner clarification, binding to `127.0.0.1` alone is explicitly
  **not** treated as sufficient. A `127.0.0.1`-bound server is still
  reachable by anything running in the user's own browser on that
  machine (a malicious page's fetch/form, a rogue extension), which is a
  real cross-site-request risk once there's no auth check to block it.
  Before this phase is accepted as done, document and apply a concrete
  mitigation — candidates to evaluate (not a commitment to all of them):
  restrictive CORS allowing only the packaged frontend's own origin,
  rejecting requests whose `Origin`/`Referer` doesn't match, and/or a
  per-launch local secret the Tauri shell generates and injects into the
  frontend (Phase 3) to present on every request. Pick based on an
  explicit risk write-up, not by default.
- Decide the **local database's relationship to a later-connected cloud
  account**: when a user eventually signs in from a previously
  account-less local install, what happens to the galleries already
  created under the synthetic local-owner identity? (Proposed: they become
  that cloud account's local-only content, `OwnerId` reassigned — directly
  analogous to today's existing "first admin account claims legacy
  ownerless galleries" mechanism in `AuthController.AdminRegister`, which
  is a close precedent for exactly this kind of one-time ownership
  migration.)
- **Local library export**, new in this revision per owner decision 2
  above: a user-triggered export of an entire local library (or a
  selected gallery) to a single ZIP file containing the original photo
  files unmodified, plus a JSON manifest describing gallery structure,
  sort order, tags, titles/descriptions, and other metadata exactly as
  stored locally. The manifest carries an explicit format-version field
  (e.g. `"formatVersion": 1`) and ships with a documented, plain-language
  schema description — not just something inferred from code — so future
  revisions (e.g. an additional XMP sidecar export, explicitly deferred
  per owner decision 2, not built now) can extend it without breaking
  older exports. **Scope for this phase is export only**; import/restore-
  from-export is a separate, not-yet-committed capability and should not
  be assumed or half-built alongside it.
  - Per owner clarification, the manifest's field list must be checked
    against the actual `Photo`/`Gallery`/`Tag` EF Core models at
    implementation time, not assumed from this plan. As of this audit
    (`backend/Models/Photo.cs`, `Gallery.cs`, `Tag.cs`), the
    **user-meaningful** fields to include are: `Photo.Title`,
    `Description`, `ExifJson` (the raw EXIF group/tag data already
    extracted at upload time), `Latitude`/`Longitude`, `SortOrder`,
    `CreatedAtUtc`/`UpdatedAtUtc`, and its `Tags` (by name); `Gallery.Title`,
    `Description`, `SortOrder`, `CreatedAtUtc`/`UpdatedAtUtc`, and its
    `Tags`; plus the gallery→photo structure itself (which photos belong
    to which gallery, in what order). Fields that are **deliberately
    excluded** as internal/not meaningful outside this specific database —
    `Id`/`GalleryId` (server-assigned, re-derived on import if import is
    ever built), `ClientTempId` (sync plumbing), `RowVersion`
    (EF concurrency token), `IsDeleted` (soft-delete only ever queries
    exclude it anyway), `HasOriginal`/`HasThumbnail` (derived from the
    files the export already includes) — should be named explicitly in
    the manifest's documentation as intentionally omitted, not silently
    dropped, in case that judgment needs revisiting later. This list must
    be re-verified against the models at the time Phase 2 is actually
    implemented, since the schema may have changed by then.

**Dependencies**: Phase 1 (so the frontend doesn't care which backend mode
it's talking to).

**Risks**: **medium**. The local-owner-identity decision affects every
`[Authorize]`-gated endpoint's behavior and needs careful review — getting
it wrong risks either breaking ownership isolation (if "local mode" leaks
into shared-instance deployments) or breaking the existing shared-instance
Tailscale deployment model (if the auth bypass isn't cleanly scoped to
local mode only). Mitigate by making local-mode an explicit, separate
build/launch configuration, not a runtime toggle on the same deployed
instance that also serves shared Tailscale users.

**Data migration**: none yet — this phase introduces a *new* way to run
the backend; it does not migrate any existing shared-instance data.

**Acceptance criteria**:
- The existing backend, unmodified in its Docker/shared-instance mode,
  continues to work exactly as today.
- A new local-mode launch path starts the backend with no auth required,
  against a local SQLite file, and the existing Angular frontend (pointed
  at it per Phase 1) works fully — create/upload/organize/browse/tag,
  offline-tolerant exactly as it already is, since "offline" and "no
  cloud account" become nearly the same state in local mode.
- Exporting a real local library with multiple galleries, photos, tags,
  and a non-trivial sort order produces a ZIP whose JSON manifest can be
  parsed back and validated against its documented schema version.

---

## Phase 3 — Desktop packaging

**Goal**: an installable desktop application wrapping Phase 2's local-mode
backend + the existing Angular frontend, per the recommendation in
`ARCHITECTURE-PROPOSED.md` §5.2 (Tauri shell + locally-run ASP.NET Core
backend + existing Angular UI in a WebView).

**Required changes**:
- A minimal Tauri project: process-lifecycle management (start the local
  backend as a child process on launch, stop it cleanly on quit — ASP.NET
  Core needs a graceful-shutdown signal handled, not a hard kill, so
  in-flight SQLite writes aren't interrupted), a window pointed at the
  local backend's `localhost` URL, a system-tray/launcher icon.
- An **installer for Windows first** (the team's primary platform —
  macOS/Linux installers follow once Windows is validated, not required
  for the pilot): Tauri's bundler handles the MSI/NSIS mechanics; what's
  new here is validating the ASP.NET Core runtime's distribution story,
  specifically whether to bundle a self-contained .NET runtime per install
  or depend on a shared one — self-contained is the safer default for an
  end-user-facing installer, at a larger download-size cost.
  - Per owner clarification, **code-signing is explicitly not required
    for the pilot** — the pilot ships to a small group of trusted
    testers, not the general public. What *is* required for the pilot:
    a defined distribution-and-verification method so testers can
    confirm they received an unmodified installer (e.g. a published
    SHA-256 checksum alongside the download link, verified manually).
    Code-signing/notarization requirements are deliberately deferred to
    be revisited **before the public release**, not before the pilot.
- A **file/folder picker** for choosing where the local library lives
  (first-run setup), using Tauri's own filesystem dialog APIs rather than
  anything browser-sandboxed.
- **Manual updates for the pilot.** Per owner clarification, the
  auto-update mechanism (Tauri's built-in updater plugin) is deferred to
  the public-release phase, not built for the pilot. The pilot instead
  needs a documented manual-update procedure (download the new
  installer, reinstall over the existing local install) that is verified
  not to lose the local database or photo folder in the process —
  reinstalling must be safe, even though it isn't automatic.

**Dependencies**: Phase 2 (a working local-mode backend to wrap).

**Risks**: **medium-high**, concentrated in territory not yet validated by
any part of the current codebase: whether the existing Angular build's
routing/base-href assumptions behave correctly inside a Tauri WebView
pointed at `localhost` (flagged, not yet confirmed, in
`PROJECT-ANALYSIS.md` §11); cross-platform ASP.NET Core child-process
lifecycle edge cases (zombie processes if the Tauri shell crashes without
cleanly stopping its child). Code-signing/notarization (macOS Gatekeeper,
Windows SmartScreen) is **not a pilot risk** per owner clarification —
it's explicitly deferred and re-assessed before the public release,
so it should not block or slow down the pilot's Phase 3 work.

**Data migration**: none — first-run setup creates a fresh local database;
nothing to migrate from in a pure-desktop-first install.

**Acceptance criteria**:
- An installable Windows package exists (unsigned is acceptable for the
  pilot, per owner clarification — cross-platform and signed builds can
  follow later) that installs, launches, creates a local library on first
  run, and supports the full existing feature set (minus anything that was
  already server-shared-instance-specific, like the bookmarklet's relay
  page, which needs its own local-mode review, not assumed to port
  unchanged).
- A published checksum (or equivalent) lets a pilot tester verify the
  installer they received is unmodified.
- Quitting the app cleanly stops the local backend process (verified via
  OS process inspection, not just "the window closed").
- Works with the network disconnected, from first launch — no feature
  degrades.
- A manual update (reinstalling a newer build over an existing install)
  has been tested at least once and does not lose the local database or
  photo folder.

---

## Phase 4 — Cloud functionality for the Social track *(ON HOLD until Track A is validated)*

Per owner decision 1, this phase does not start until the Desktop MVP
(Phases 1–3) is built and proven reliable. It is split into two sub-phases
that were previously conflated: a cheap, existing-infrastructure pilot
(4A) and the full independent-cloud build-out (4B) that a real public/
investor-facing product eventually needs. 4A does not require 4B — a
small friend pilot does not need PostgreSQL, object storage, or a second
deployed service.

### Phase 4A — Social pilot on existing infrastructure

**Goal**: validate the social feature set (Friendship, gallery
Subscriptions, the independent-permissions/`AudienceLevel` model, the
Activity Feed, and Discovery's extension to friends' content — detailed in
the separate concept/design review, not re-specified here) with a small
group of real users, without standing up any new infrastructure.

**Required changes**:
- The social data model and API surface, built directly into the
  **existing** ASP.NET Core backend, existing SQLite database, and
  existing Docker Compose + Tailscale deployment — no new service, no new
  database engine, no object storage. Photos continue to live exactly
  where they do today.
- Sized explicitly for **3–5 initial participants**, with headroom to
  **10–20** if that proves stable, per owner decision 3 — not designed or
  tested against any larger load.

**Dependencies**: Phase 1 (clean API boundary) only — explicitly does
**not** depend on Phase 2 or 3 (Desktop), since the pilot runs on the
existing shared web app. Sequenced after Track A by owner decision, not by
a technical dependency.

**Risks**: **low-to-medium**, and deliberately scoped down from the
original Phase 4's "high" rating because this sub-phase reuses proven,
already-deployed infrastructure instead of introducing a second service.
The one accepted risk — the shared host (and therefore the pilot) being
unreachable when that machine or its network is down — is, per owner
decision 3, **treated as a known limitation of the test environment, not
a defect to fix before launch.** This acceptance applies only at pilot
scale; it does not carry over to Phase 4B.

**Data migration**: none — new tables only, additive to the existing
schema.

**Acceptance criteria**:
- 3–5 real participants can use the pilot's social features (follow/
  subscribe, see each other's shared galleries per the granted
  permissions, see a personalized Discovery feed that includes friends'
  content, see an Activity Feed of relevant events) against the existing
  shared backend, with no new infrastructure stood up.
- The owner has explicitly signed off that the pilot's feature set (not
  just its infrastructure) is ready to validate with real people — this
  plan governs sequencing/infrastructure, not the detailed feature spec,
  which is tracked separately.

---

### Phase 4B — Independent cloud storage for published content (full scale)

**Goal**: a real, separately-deployed cloud backend exists, with its own
multi-tenant database and blob/object storage, able to durably hold
published content independent of any desktop install being online —
per `ARCHITECTURE-PROPOSED.md` §1/§3. Gated on Phase 4A having validated
that the social feature set works and that demand/stability justify
dedicated infrastructure spend — e.g. an investor-facing demo, more than
~20 participants, or a genuine always-on guarantee the shared Tailscale
host cannot promise.

**Required changes**:
- **Hard prerequisite**: migrate off SQLite to PostgreSQL for this cloud
  deployment specifically (`TODO,md`'s existing Phase 5 item) — SQLite's
  single-writer model, already flagged as a known limitation, is
  disqualifying for a real multi-tenant cloud database, not just a
  someday-nicety.
- A new cloud-specific data model: accounts, published-content index,
  derivative-tier metadata (§5 of `ARCHITECTURE-PROPOSED.md`) — related to
  but **not identical** to the local `Gallery`/`Photo` schema (the cloud
  needs `Visibility`, publish timestamps, and derivative-generation status
  that pure local storage never needs).
- Object/blob storage integration (S3-compatible — AWS S3, Cloudflare R2,
  Backblaze B2, or self-hosted MinIO are all viable; pick based on cost/
  egress-fee tolerance, a product decision not resolved here) for
  originals + the 3 proposed derivative tiers.
- A **derivative-generation pipeline** — likely an async job (publish
  triggers thumbnail/medium/high-res generation server-side, not
  synchronously in the publish request) since generating 3 derivative
  tiers for a batch publish shouldn't block the user's UI.
- A new **cloud backup strategy**, independent of the existing Docker
  Compose `backup` service (which assumes one SQLite file + one uploads
  folder on one host — doesn't fit a managed PostgreSQL + object-storage
  deployment at all).

**Dependencies**: Phase 4A (the pilot must have validated the social
feature set first — see this phase's gating condition above). Not
dependent on Phases 2–3 — cloud storage can be built and tested against
the existing Tailscale-shared-instance frontend before desktop packaging
exists, since "publish to cloud" is meaningful even from a browser-only
client. Hard external dependency on `TODO,md` Phase 5 (PostgreSQL).

**Risks**: **high** — this is the first phase introducing genuinely new
infrastructure (a second deployed service, a different database engine, a
third-party or self-hosted object store) rather than extending the
existing single backend. Cost risk (storage + egress fees) needs a
pricing/budget decision before real usage, not after. Operational risk
(a second service to monitor/back up/secure) is new surface area this
project has not previously carried.

**Data migration**: none of *existing users'* data needs migrating — this
phase stands up a new, empty cloud deployment. (The existing SQLite→
PostgreSQL migration, if also applied to any *shared-instance* deployment
rather than just this new cloud one, is `TODO,md` Phase 5's own concern
and has its own data-migration story, orthogonal to this plan.)

**Acceptance criteria**:
- A photo published from a test account is fetchable via the cloud API
  with the test account's originating device fully powered off/
  disconnected — the literal acceptance test for "published content
  survives the owner's device being off."
- All 4 derivative tiers (§5.1) are generated and served with correct
  content-negotiation (`Accept` header → WebP/AVIF/JPEG).
- The cloud backup strategy has been exercised at least once (a real
  restore-from-backup drill, not just "the backup job ran without error").

---

## Phase 5 — Implement selective synchronization and publishing

**Goal**: a Desktop (or any client) can connect to a cloud account and
explicitly publish/sync content, per the five distinct operations in
`ARCHITECTURE-PROPOSED.md` §3.2, without conflating them.

**Required changes**:
- Sign-in flow connecting a local Desktop install (or, if this phase lands
  before Phase 3, the existing web client) to a cloud account — the local
  database gains a nullable "connected cloud account id," not a
  requirement.
- **Publish** UI/flow: select a local gallery, choose a `Visibility` level,
  trigger upload of the original + await derivative generation (Phase 4B),
  record the assigned cloud id back onto the local `Gallery`/`Photo`
  records (§3.3's "cloud assigns the id, never the device").
- **Private cross-device sync** flow: extend (not replace) the existing
  `SyncCoordinatorService`/outbox shape to target "my cloud account" as
  the sync peer instead of "the one shared backend" — the existing
  conflict-detection logic (`updatedAtUtc` comparison) is directly
  reusable per `ARCHITECTURE-PROPOSED.md` §6.2.
- **Resumable/chunked upload** for large originals (§6.3 of the
  architecture doc) — a genuinely new capability, not a tuning change to
  the existing 3-retry logic.
- Structural prevention of domains 3/4 (another user's content, saved
  references) ever entering the mutation outbox — a read-only reference
  table instead (§6.1).

**Dependencies**: Phase 2 (local-first storage model — publishing *from*
something requires that something to exist independently first), Phase 4B
(somewhere durable to publish *to*). Does not strictly require Phase 3 (desktop
packaging) — publishing could be validated from the existing web client
against local-mode-Phase-2 data first, though the full product vision
assumes Desktop exists by this point.

**Risks**: **high** — this phase is where the four-domain distinction
(§6.1 of the architecture doc) either holds up under real implementation
pressure or doesn't; the biggest risk is schedule pressure leading to
"just reuse the existing outbox for everything," which would silently
reintroduce the single-domain conflation this whole plan exists to avoid.
Concurrent-edit conflict UX (two devices, same cloud-synced photo, a
genuine conflict) needs real user testing, not just the existing
single-backend sync-review modal assumed to generalize unchanged.

**Data migration**: existing shared-Tailscale-instance users' data is
**not automatically migrated to cloud accounts** — that deployment model
continues to exist independently (see §"What doesn't change" below) unless
a specific user chooses to connect and publish from it.

**Acceptance criteria**:
- A gallery published from device A is visible (per its visibility level)
  via the cloud API with device A offline.
- The same cloud account, connected from device B, can selectively sync
  down the published gallery without having to re-publish it.
- A genuine edit conflict (same cloud-synced photo edited on A and B while
  both briefly offline from each other) surfaces as an explicit review
  item, not a silent last-write-wins overwrite.
- Revoking a share (§3.4) stops new access within a defined, tested time
  bound (e.g. next request, not next cache-expiry-whenever).

---

## Phase 6 — Implement social functionality (full scale)

**Goal**: friends/following, cross-user feeds, saving others' photos — the
"Social" half of `ARCHITECTURE-PROPOSED.md` §2.B, hardened for public/
investor-facing scale. Much of this feature set is **not new work** by
this point — Phase 4A already built and piloted the Friendship/visibility/
Discovery-extension model on existing infrastructure; this phase is
primarily about moving that already-validated model onto Phase 4B's
durable infrastructure and hardening it for abuse/scale concerns the small
pilot didn't need to face (see Risks below). The detailed permission-model
shape (independent capability flags, not a single `Visibility` level) was
refined in a separate design review after this plan was first written —
treat the bullets below as the still-valid *goal*, not the final schema.

**Required changes**:
- `Follow`/`Friendship` data model (direction matters — "following" is
  typically one-directional with an optional "friends" = mutual-follow
  convention, a product decision to make explicitly, not default into).
- Extend `DiscoveryFeedService`'s existing tag-affinity weighting
  (`PhotoView`-based, already live and single-user today — direct reuse,
  not a rewrite) to include friends'/followed-public content in its
  candidate pool, respecting visibility per request.
- "Save to my feed" flow implementing §3.5's reference-with-attribution
  model, including the tombstone-on-revocation behavior.
- Per-request permission checks on every cross-user read path — this is
  where `GalleryAccessService`'s existing "ownership check, 404 if not
  owned" pattern needs a genuine extension (not replacement) to "ownership
  OR granted-visibility check."

**Dependencies**: Phase 4A (already satisfied — the pilot is what proves
this feature set out), Phase 5 (publishing/visibility must exist at full
scale before "friends can see published content" is meaningful beyond the
pilot).

**Risks**: **high** — this is the phase most exposed to real-world misuse
concerns (harassment via unwanted follows, content scraping of public
feeds, revocation not propagating fast enough to matter) that have no
precedent in the current single-tenant-per-instance, Tailscale-only app.
Needs explicit product/moderation-policy decisions this document doesn't
make (report/block mechanisms, rate limits on public-feed scraping) before
launch, not just the engineering described here.

**Data migration**: none for existing users beyond what Phase 5 already
introduced (a nullable cloud-account link) — following/friend graphs start
empty for everyone.

**Acceptance criteria**:
- Two test cloud accounts can follow each other, and User B's Discovery
  feed includes User A's public photos with correct attribution.
- A save-to-feed action by B, followed by A deleting the original, results
  in B's reference becoming a visible tombstone (not a silent broken
  image, not a continued-serving stale copy) — unless A's grant included
  download-original, in which case B's independent copy is unaffected, and
  this distinction is verified, not assumed.
- Revoking A's public visibility stops it appearing in B's feed on B's
  next load.

---

## Phase 7 — Introduce the browser-based cloud interface (Web)

**Goal**: `ARCHITECTURE-PROPOSED.md` §2.C — a browser client of the cloud
API, independent of any desktop install.

**Required changes**:
- Likely the **least net-new frontend work** of any phase: the existing
  Angular app, already built against the Phase-1 clean API boundary, is a
  strong starting point — the open question is whether Web should be
  *the same Angular codebase* pointed at the cloud API (reusing local-
  library-specific UI it doesn't need, like local-folder pickers) or a
  trimmed build. Recommend starting with the same codebase pointed at the
  cloud API and *removing* desktop-only affordances behind a build flag,
  rather than forking a second frontend project — directly consistent
  with this plan's Phase-1 "one clean seam" investment paying off here.
- A logged-out public-browsing mode (profiles, public feeds/collections)
  — new, since today's `LoginComponent` is a full-screen, non-dismissible
  gate with no logged-out browsing path at all (confirmed in
  `PROJECT-ANALYSIS.md` §6).
- Server-side rendering or at least solid meta-tag/Open-Graph support for
  public profile/collection pages, if discoverability/link-sharing matters
  for the social product — a product decision affecting scope here,
  flagged rather than resolved.

**Dependencies**: Phase 6 (social functionality should exist before a
public-facing web surface for it is built, or Web ships with Discovery/
feeds but no follow/friend UI yet as a smaller first cut — a legitimate
scope-reduction option, not a hard blocker either way).

**Risks**: **medium** — technically the most straightforward phase given
Phase 1's groundwork, but carries the most *public-facing* exposure
(performance under real anonymous traffic, SEO/crawlability if public
content is meant to be discoverable externally, abuse-resistance for a
logged-out-accessible surface) of any phase so far.

**Data migration**: none.

**Acceptance criteria**:
- A logged-out visitor can browse a public profile/collection with zero
  PhotoLib installation, via a shared link.
- A signed-in Web user has full parity with Desktop for everything that
  isn't inherently local (following, publishing-status viewing, feed
  browsing) — explicitly *not* parity for local-file-folder management,
  which doesn't apply to a browser client.

---

## What doesn't change, throughout

Explicitly, to set expectations for anyone reading this plan mid-execution:

- The existing Docker Compose / Tailscale-shared-instance deployment model
  (today's actual production use) **keeps working, unmodified, through
  every phase above**, unless/until a specific future decision is made to
  sunset it in favor of "everyone runs Desktop + an optional cloud
  account" instead. This plan does not assume that decision — it's a
  genuinely open product question, not an engineering one.
- No phase requires deleting or migrating existing shared-instance users'
  galleries/photos against their will.
- `TODO,md`'s existing SaaS/Monetization phases continue independently —
  they intersect with this plan (shared PostgreSQL dependency, shared
  "real user accounts" foundation that's already done) but are not
  subsumed by it; billing a shared Tailscale instance's AI feature and
  running a social cloud platform are different products that happen to
  share some infrastructure.
- Phase 4A's pilot being occasionally unreachable (when the shared host or
  its network is down) is **accepted at pilot scale only**, per owner
  decision 3 — it is a statement about this specific test environment,
  not a claim that the target architecture tolerates published content
  being unavailable. Phase 4B exists precisely to remove that limitation
  once the product needs to make that guarantee for real.
