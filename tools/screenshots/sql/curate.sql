-- Turns a full copy of a development database into the screenshot fixture.
-- Runs against a throwaway staging database; the source database is only ever read by pg_dump.
--
-- psql variables:
--   password_hash   ASP.NET Identity hash of the fixture login password
--   login_email     e-mail address of the fixture login
--   catalog_size    newest unplayed maps kept so the catalog has more than the played maps
--   motion_sessions latest finished plays that keep their raw saber motion frames
\set ON_ERROR_STOP on

BEGIN;

-- The account with the most plays becomes the fixture account; everyone else goes.
DELETE FROM "Users"
WHERE "Id" <> (
    SELECT u."Id"
    FROM "Users" u
    LEFT JOIN "PlaySessions" s ON s."UserId" = u."Id"
    GROUP BY u."Id"
    ORDER BY count(s."Id") DESC, u."Id"
    LIMIT 1
);

-- Credentials, secrets and personal details are replaced, display name, handle and artwork stay.
UPDATE "Users" SET
    "Email" = :'login_email',
    "NormalizedEmail" = upper(:'login_email'),
    "UserName" = 'demo',
    "NormalizedUserName" = 'DEMO',
    "EmailConfirmed" = true,
    "PasswordHash" = :'password_hash',
    "SecurityStamp" = 'SCREENSHOTFIXTURESECURITYSTAMP00',
    "ConcurrencyStamp" = '00000000-0000-0000-0000-000000000000',
    "PhoneNumber" = NULL,
    "PhoneNumberConfirmed" = false,
    "TwoFactorEnabled" = false,
    "LockoutEnd" = NULL,
    "AccessFailedCount" = 0,
    "HeightCm" = CASE WHEN "HealthTrackingEnabled" THEN 180 END,
    "WeightKg" = CASE WHEN "HealthTrackingEnabled" THEN 75 END,
    "BirthYear" = CASE WHEN "HealthTrackingEnabled" THEN 1998 END,
    "Sex" = NULL,
    "BodyFatPercent" = NULL,
    "RestingHeartRate" = NULL;

DELETE FROM "UserClaims";
DELETE FROM "UserLogins";
DELETE FROM "UserTokens";
DELETE FROM "RefreshTokens";
DELETE FROM "SensorSamples";
DELETE FROM "HealthProxyClients";
UPDATE "Devices" SET "ClientId" = '00000000-0000-0000-0000-000000000001';

-- Played, liked and listed maps, plus the newest part of the catalog.
CREATE TEMP TABLE keep_maps AS
SELECT d."BeatmapId" AS "Id"
FROM "PlaySessions" s
JOIN "BeatmapDifficulties" d ON d."Id" = s."BeatmapDifficultyId"
UNION SELECT "BeatmapId" FROM "MapLikes"
UNION SELECT "BeatmapId" FROM "MapListItems"
UNION (SELECT "Id" FROM "Beatmaps" ORDER BY "CreatedAt" DESC LIMIT :catalog_size);

DELETE FROM "Beatmaps" WHERE "Id" NOT IN (SELECT "Id" FROM keep_maps);
DELETE FROM "BeatSaverUsers" u
WHERE NOT EXISTS (SELECT 1 FROM "BeatSaverMaps" m WHERE m."UploaderId" = u."Id");

-- Raw motion frames dominate the dump. Only the latest finished plays (EndReason 1), the
-- ones the play list opens with, keep them; summaries are kept for every play.
DELETE FROM "PlaySessionItemMotionFrames"
WHERE "PlaySessionId" NOT IN (
    SELECT "Id" FROM "PlaySessions" WHERE "EndReason" = 1 ORDER BY "StartedAt" DESC LIMIT :motion_sessions
);

COMMIT;
