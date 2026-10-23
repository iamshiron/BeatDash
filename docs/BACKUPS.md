# Admin backups

Backup/restore is CLI-only and uses the authenticated BeatDash backend, never direct infrastructure access.

## Prepare

1. Stop all normal API instances and other writers. Start **one** instance with `Backup__MaintenanceMode=true`.
   Regular traffic, UDP ingestion, and background jobs are disabled.
2. Set `Backup__WorkDirectory` to a **private, persistent directory outside the web root**. Retain it across restarts
   and reserve space for the uploaded archive, a full recovery snapshot, and the largest payload.
3. Use HTTPS for remote access (HTTP is accepted only on loopback). Configure proxy upload limits/timeouts if needed.

## Export / restore

```sh
# Password is prompted securely, or read from BEATDASH_ADMIN_PASSWORD.
dotnet run --project src/BeatDash.Cli -- backup export ./backup.zip \
  --host https://beatdash.example.com --email admin@example.com --scope Both

dotnet run --project src/BeatDash.Cli -- backup restore ./backup.zip \
  --host https://beatdash.example.com --email admin@example.com --scope Both
```

- **Both** (default): complete system data backup.
- **Database**: all application and Identity records, relationships, IDs, and identifier sequences.
- **Storage**: all current objects in the two configured application buckets, with keys, media types, and bytes.
- No external filesystem files, deployment secrets/configuration, binaries, database schema, migration history,
  or bucket policies/version history are included.
- **Restore replaces, not merges.** Missing objects are deleted. Partial scopes leave the other component untouched
  and may break cross-component references. Restore asks for confirmation; unattended use requires `--yes`.
- Database restore requires a pre-provisioned **matching application schema and PostgreSQL major version**.
  Know an administrator's credentials **from the backup**, because restoring Identity replaces target accounts.
- Every request rechecks current admin membership and security stamp. Ordinary accounts, cookies, and device tokens
  cannot access backups. For two-factor-enabled accounts, set `BEATDASH_ADMIN_TWO_FACTOR_CODE` to a current authenticator code.

Archives are versioned and checksum-validated before mutation. They contain credentials and private data:
encrypt/protect them externally, keep off-server copies, and restore only trusted archives. Checksums are not signatures.
The default archive/uncompressed size limit is 1 TiB (`Backup__MaxArchiveBytes`).

## Interrupted restore

A private `recovery.zip` preserves pre-restore state until success. Client disconnection does not cancel replacement.
If restoration fails or the server crashes, normal startup and new backups/restores are blocked. Keep maintenance
mode and the **same persistent workspace**, fix the underlying issue, then restore the pre-operation state:

```sh
dotnet run --project src/BeatDash.Cli -- backup recover \
  --host https://beatdash.example.com --email admin@example.com
```

Recovery is retryable. Authenticate using an administrator currently in the database. Do not delete `recovery.zip`.
After successful restore/recovery, restart with `Backup__MaintenanceMode=false`.

## Tests

Run `dotnet test tests/BeatDash.API.Tests --configuration Release -p:SkipWebBuild=true`.
Optional integration tests require **disposable** infrastructure: `BEATDASH_TEST_POSTGRES` (connection string with
CREATE DATABASE permission) and `BEATDASH_TEST_RUSTFS` (`host:port`, credentials `backup-test` / `backup-test-secret`).
They verify every table/sequence, storage bytes/media types, scope isolation, authorization, and CLI/API round trips.
