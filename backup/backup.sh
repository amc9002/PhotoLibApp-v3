#!/bin/sh
set -eu

: "${BACKUP_INTERVAL_HOURS:=24}"
: "${BACKUP_KEEP:=14}"

DB_SRC="/data/photolib.db"
UPLOADS_SRC="/data/uploads"
DEST="/backups"

run_backup() {
  ts=$(date -u +%Y%m%d-%H%M%S)
  echo "[backup] $ts: starting"

  # sqlite3's own .backup command takes a live, consistent snapshot via the
  # SQLite backup API even while the app has the file open for writes - a
  # plain file copy could otherwise catch it mid-write.
  if [ -f "$DB_SRC" ]; then
    sqlite3 "$DB_SRC" ".backup '/tmp/photolib-$ts.db'"
    gzip -c "/tmp/photolib-$ts.db" > "$DEST/photolib-db-$ts.db.gz"
    rm -f "/tmp/photolib-$ts.db"
  fi

  if [ -d "$UPLOADS_SRC" ]; then
    tar -czf "$DEST/photolib-uploads-$ts.tar.gz" -C /data uploads
  fi

  echo "[backup] $ts: done"

  # Prune: keep only the BACKUP_KEEP most recent of each type.
  for prefix in photolib-db- photolib-uploads-; do
    # shellcheck disable=SC2086
    ls -1t "$DEST"/$prefix*.gz 2>/dev/null | tail -n "+$((BACKUP_KEEP + 1))" | xargs -r rm -f
  done
}

while true; do
  # A single failed cycle (e.g. transient disk pressure) shouldn't crash
  # the container and trigger a fast restart-loop - log it and try again
  # next interval instead.
  run_backup || echo "[backup] cycle failed, will retry next interval"
  sleep "$((BACKUP_INTERVAL_HOURS * 3600))"
done
