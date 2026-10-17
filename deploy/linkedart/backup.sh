#!/usr/bin/env bash
# Nightly snapshot of the linkedart.net data that is not in git: the trainer's queue and submissions and the dates
# service's capture file. Installed as /usr/local/bin/linkedart-backup and run by /etc/cron.d/linkedart-backup.
#
#   linkedart-backup            make a snapshot, prune old ones, print a line
#   linkedart-backup --list     show the snapshots and what the newest contains
#
# A snapshot is a gzipped tar of the data directory, a few kilobytes. The newest is also reachable as
# $BACKUP_DIR/latest.tar.gz. Off-box copies: see deploy/linkedart/pull-backup.ps1 and docs/09-hosting.md section 8.
set -euo pipefail

SOURCE_DIR="${LINKEDART_DATA:-/srv/linkedart/data}"      # what to back up
BACKUP_DIR="${LINKEDART_BACKUPS:-/srv/backups/linkedart}" # where snapshots go
KEEP_DAYS="${LINKEDART_KEEP_DAYS:-30}"                    # prune snapshots older than this

if [[ "${1:-}" == "--list" ]]; then
  ls -lh "$BACKUP_DIR"/data-*.tar.gz 2>/dev/null || { echo "no snapshots in $BACKUP_DIR"; exit 0; }
  echo "--- newest:"
  tar -tzvf "$BACKUP_DIR/latest.tar.gz"
  exit 0
fi

[[ -d "$SOURCE_DIR" ]] || { echo "linkedart-backup: $SOURCE_DIR does not exist" >&2; exit 1; }
mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

stamp="$(date -u +%Y%m%d-%H%M)"
target="$BACKUP_DIR/data-$stamp.tar.gz"
# -C so the archive holds "data/..." paths; restoring is "tar -xzf ... -C /srv/linkedart".
tar -czf "$target.tmp" -C "$(dirname "$SOURCE_DIR")" "$(basename "$SOURCE_DIR")"
mv "$target.tmp" "$target"
ln -sfn "$(basename "$target")" "$BACKUP_DIR/latest.tar.gz"

# Keep the last KEEP_DAYS days; the data is tiny, so this is about tidiness, not space.
find "$BACKUP_DIR" -name 'data-*.tar.gz' -type f -mtime "+$KEEP_DAYS" -delete

count="$(find "$BACKUP_DIR" -name 'data-*.tar.gz' -type f | wc -l)"
echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) linkedart-backup: wrote $(basename "$target") ($(du -h "$target" | cut -f1)), $count snapshots kept"
