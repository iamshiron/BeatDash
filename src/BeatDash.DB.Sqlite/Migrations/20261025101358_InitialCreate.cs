using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BeatSaverUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeatSaverUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Avatar = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Admin = table.Column<bool>(type: "INTEGER", nullable: false),
                    Curator = table.Column<bool>(type: "INTEGER", nullable: false),
                    SeniorCurator = table.Column<bool>(type: "INTEGER", nullable: false),
                    PlaylistUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeatSaverUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Handle = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ProfileStatsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileActivityPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileSkillPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileHistoryPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileListsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileLikedPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    AvatarKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    BannerKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    HealthTrackingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    HeightCm = table.Column<int>(type: "INTEGER", nullable: true),
                    WeightKg = table.Column<double>(type: "REAL", nullable: true),
                    BirthYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Sex = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    BodyFatPercent = table.Column<double>(type: "REAL", nullable: true),
                    RestingHeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Beatmaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LevelId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    SongName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SongSubName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SongAuthor = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Mapper = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Bpm = table.Column<float>(type: "REAL", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    CoverImageKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FetchStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchLastAttemptedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FetchAttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchError = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beatmaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Beatmaps_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HealthProxyClients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthProxyClients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthProxyClients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapLists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayNoteAggregates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacteristicSerializedName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ColorType = table.Column<int>(type: "INTEGER", nullable: false),
                    CutDirection = table.Column<int>(type: "INTEGER", nullable: false),
                    LineIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    NoteLineLayer = table.Column<int>(type: "INTEGER", nullable: false),
                    NoteCount = table.Column<long>(type: "INTEGER", nullable: false),
                    GoodCount = table.Column<long>(type: "INTEGER", nullable: false),
                    MissCount = table.Column<long>(type: "INTEGER", nullable: false),
                    BadCount = table.Column<long>(type: "INTEGER", nullable: false),
                    SumEarnedScore = table.Column<long>(type: "INTEGER", nullable: false),
                    SumMaxScore = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayNoteAggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayNoteAggregates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeatmapDifficulties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DifficultyRank = table.Column<int>(type: "INTEGER", nullable: false),
                    DifficultyName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NotesPerSecond = table.Column<float>(type: "REAL", nullable: false),
                    CuttableObjectCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BombCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ObstacleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LaneCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NoteJumpSpeed = table.Column<float>(type: "REAL", nullable: true),
                    CharacteristicSerializedName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CharacteristicColorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicRequires360Movement = table.Column<bool>(type: "INTEGER", nullable: false),
                    CharacteristicContainsRotationEvents = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BeatmapId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeatmapDifficulties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatmapDifficulties_Beatmaps_BeatmapId",
                        column: x => x.BeatmapId,
                        principalTable: "Beatmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BeatmapDifficulties_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BeatSaverMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeatSaverId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Automapper = table.Column<bool>(type: "INTEGER", nullable: false),
                    Ranked = table.Column<bool>(type: "INTEGER", nullable: false),
                    Qualified = table.Column<bool>(type: "INTEGER", nullable: false),
                    BlRanked = table.Column<bool>(type: "INTEGER", nullable: false),
                    BlQualified = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeclaredAi = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Uploaded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BeatSaverCreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BeatSaverUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ZipObjectKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metadata_Bpm = table.Column<float>(type: "REAL", nullable: false),
                    Metadata_Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Metadata_SongName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Metadata_SongSubName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Metadata_SongAuthorName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Metadata_LevelAuthorName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Stats_Plays = table.Column<int>(type: "INTEGER", nullable: false),
                    Stats_Downloads = table.Column<int>(type: "INTEGER", nullable: false),
                    Stats_Upvotes = table.Column<int>(type: "INTEGER", nullable: false),
                    Stats_Downvotes = table.Column<int>(type: "INTEGER", nullable: false),
                    Stats_Score = table.Column<float>(type: "REAL", nullable: false),
                    UploaderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BeatmapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeatSaverMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatSaverMaps_BeatSaverUsers_UploaderId",
                        column: x => x.UploaderId,
                        principalTable: "BeatSaverUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BeatSaverMaps_Beatmaps_BeatmapId",
                        column: x => x.BeatmapId,
                        principalTable: "Beatmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapLikes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeatmapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapLikes_Beatmaps_BeatmapId",
                        column: x => x.BeatmapId,
                        principalTable: "Beatmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Expires = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SensorSamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Metric = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorSamples_HealthProxyClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "HealthProxyClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SensorSamples_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MapListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeatmapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapListItems_Beatmaps_BeatmapId",
                        column: x => x.BeatmapId,
                        principalTable: "Beatmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapListItems_MapLists_MapListId",
                        column: x => x.MapListId,
                        principalTable: "MapLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeatmapDifficultyAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    NoteCount = table.Column<int>(type: "INTEGER", nullable: true),
                    BombCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ObstacleCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ChainCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ArcCount = table.Column<int>(type: "INTEGER", nullable: true),
                    BpmChangeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Bpm = table.Column<double>(type: "REAL", nullable: true),
                    Njs = table.Column<double>(type: "REAL", nullable: true),
                    NjsOffset = table.Column<double>(type: "REAL", nullable: true),
                    FormatVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FeatureStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Features = table.Column<string>(type: "TEXT", nullable: true),
                    MetricStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DifficultyRating = table.Column<double>(type: "REAL", nullable: true),
                    Pp = table.Column<double>(type: "REAL", nullable: true),
                    Characteristics = table.Column<string>(type: "TEXT", nullable: true),
                    MetricConfigHash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    AnalyzerVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BeatmapDifficultyId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeatmapDifficultyAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatmapDifficultyAnalyses_BeatmapDifficulties_BeatmapDifficultyId",
                        column: x => x.BeatmapDifficultyId,
                        principalTable: "BeatmapDifficulties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeatmapDifficultyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AutoMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    EndReason = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifierFlags = table.Column<int>(type: "INTEGER", nullable: true),
                    Results_Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Results_MultipliedScore = table.Column<int>(type: "INTEGER", nullable: true),
                    Results_MaxPossibleScore = table.Column<int>(type: "INTEGER", nullable: true),
                    Results_Accuracy = table.Column<float>(type: "REAL", nullable: true),
                    Results_Rank = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    Results_FullCombo = table.Column<bool>(type: "INTEGER", nullable: true),
                    Results_MaxCombo = table.Column<int>(type: "INTEGER", nullable: true),
                    Results_GoodCuts = table.Column<int>(type: "INTEGER", nullable: true),
                    Results_BadCuts = table.Column<int>(type: "INTEGER", nullable: true),
                    Results_Misses = table.Column<int>(type: "INTEGER", nullable: true),
                    Results_FinalEnergy = table.Column<float>(type: "REAL", nullable: true),
                    Results_EndSongTimeMs = table.Column<int>(type: "INTEGER", nullable: true),
                    AggregatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessions_BeatmapDifficulties_BeatmapDifficultyId",
                        column: x => x.BeatmapDifficultyId,
                        principalTable: "BeatmapDifficulties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaySessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeatSaverVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SageScore = table.Column<int>(type: "INTEGER", nullable: true),
                    DownloadUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    PreviewUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    BeatSaverMapId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeatSaverVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatSaverVersions_BeatSaverMaps_BeatSaverMapId",
                        column: x => x.BeatSaverMapId,
                        principalTable: "BeatSaverMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionComboBreakItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SongTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ComboBefore = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessionComboBreakItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionComboBreakItems_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionEnergyChangeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SongTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Energy = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessionEnergyChangeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionEnergyChangeItems_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionItemMotionFrames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SongTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    FrameCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessionItemMotionFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionItemMotionFrames_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionMotionSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FrameCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SampleRateHz = table.Column<int>(type: "INTEGER", nullable: false),
                    LeftSaberTravel = table.Column<double>(type: "REAL", nullable: false),
                    RightSaberTravel = table.Column<double>(type: "REAL", nullable: false),
                    HeadTravel = table.Column<double>(type: "REAL", nullable: false),
                    AvgLeftSaberSpeed = table.Column<double>(type: "REAL", nullable: false),
                    AvgRightSaberSpeed = table.Column<double>(type: "REAL", nullable: false),
                    LeftReachRange = table.Column<double>(type: "REAL", nullable: false),
                    RightReachRange = table.Column<double>(type: "REAL", nullable: false),
                    HeadRange = table.Column<double>(type: "REAL", nullable: false),
                    FatigueCurve = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessionMotionSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionMotionSummaries_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionNoteItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SongTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ColorType = table.Column<int>(type: "INTEGER", nullable: false),
                    NoteType = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoringType = table.Column<int>(type: "INTEGER", nullable: false),
                    CutDirection = table.Column<int>(type: "INTEGER", nullable: false),
                    LineIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    NoteLineLayer = table.Column<int>(type: "INTEGER", nullable: false),
                    Result = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxScore = table.Column<int>(type: "INTEGER", nullable: false),
                    BeforeCutScore = table.Column<int>(type: "INTEGER", nullable: false),
                    CenterDistanceScore = table.Column<int>(type: "INTEGER", nullable: false),
                    AfterCutScore = table.Column<int>(type: "INTEGER", nullable: false),
                    PreCutSwing = table.Column<float>(type: "REAL", nullable: false),
                    PostCutSwing = table.Column<float>(type: "REAL", nullable: false),
                    CutPointDistance = table.Column<float>(type: "REAL", nullable: false),
                    SaberSpeed = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessionNoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionNoteItems_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionScoreChangeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SongTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessionScoreChangeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionScoreChangeItems_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeatSaverVersionDifficulties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Njs = table.Column<float>(type: "REAL", nullable: false),
                    Offset = table.Column<float>(type: "REAL", nullable: false),
                    Notes = table.Column<int>(type: "INTEGER", nullable: false),
                    Bombs = table.Column<int>(type: "INTEGER", nullable: false),
                    Obstacles = table.Column<int>(type: "INTEGER", nullable: false),
                    Nps = table.Column<float>(type: "REAL", nullable: false),
                    Length = table.Column<float>(type: "REAL", nullable: false),
                    Characteristic = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Difficulty = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Events = table.Column<int>(type: "INTEGER", nullable: false),
                    Chroma = table.Column<bool>(type: "INTEGER", nullable: false),
                    MappingExtensions = table.Column<bool>(type: "INTEGER", nullable: false),
                    NoodleExtensions = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cinema = table.Column<bool>(type: "INTEGER", nullable: false),
                    Seconds = table.Column<float>(type: "REAL", nullable: false),
                    MaxScore = table.Column<int>(type: "INTEGER", nullable: true),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ParityErrors = table.Column<int>(type: "INTEGER", nullable: false),
                    ParityWarns = table.Column<int>(type: "INTEGER", nullable: false),
                    ParityResets = table.Column<int>(type: "INTEGER", nullable: false),
                    BeatSaverVersionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeatSaverVersionDifficulties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatSaverVersionDifficulties_BeatSaverVersions_BeatSaverVersionId",
                        column: x => x.BeatSaverVersionId,
                        principalTable: "BeatSaverVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeatmapDifficulties_BeatmapId_CharacteristicSerializedName_DifficultyRank",
                table: "BeatmapDifficulties",
                columns: new[] { "BeatmapId", "CharacteristicSerializedName", "DifficultyRank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeatmapDifficulties_SubmittedByUserId",
                table: "BeatmapDifficulties",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatmapDifficultyAnalyses_BeatmapDifficultyId",
                table: "BeatmapDifficultyAnalyses",
                column: "BeatmapDifficultyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beatmaps_LevelId",
                table: "Beatmaps",
                column: "LevelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beatmaps_SubmittedByUserId",
                table: "Beatmaps",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverMaps_BeatmapId",
                table: "BeatSaverMaps",
                column: "BeatmapId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverMaps_BeatSaverId",
                table: "BeatSaverMaps",
                column: "BeatSaverId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverMaps_UploaderId",
                table: "BeatSaverMaps",
                column: "UploaderId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverUsers_BeatSaverUserId",
                table: "BeatSaverUsers",
                column: "BeatSaverUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverVersionDifficulties_BeatSaverVersionId",
                table: "BeatSaverVersionDifficulties",
                column: "BeatSaverVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverVersions_BeatSaverMapId",
                table: "BeatSaverVersions",
                column: "BeatSaverMapId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverVersions_Hash",
                table: "BeatSaverVersions",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ClientId_UserId",
                table: "Devices",
                columns: new[] { "ClientId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "Devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HealthProxyClients_TokenHash",
                table: "HealthProxyClients",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthProxyClients_UserId",
                table: "HealthProxyClients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MapLikes_BeatmapId",
                table: "MapLikes",
                column: "BeatmapId");

            migrationBuilder.CreateIndex(
                name: "IX_MapLikes_UserId_BeatmapId",
                table: "MapLikes",
                columns: new[] { "UserId", "BeatmapId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapListItems_BeatmapId",
                table: "MapListItems",
                column: "BeatmapId");

            migrationBuilder.CreateIndex(
                name: "IX_MapListItems_MapListId_BeatmapId",
                table: "MapListItems",
                columns: new[] { "MapListId", "BeatmapId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapLists_UserId",
                table: "MapLists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayNoteAggregates_UserId_CharacteristicSerializedName_ColorType_CutDirection_LineIndex_NoteLineLayer",
                table: "PlayNoteAggregates",
                columns: new[] { "UserId", "CharacteristicSerializedName", "ColorType", "CutDirection", "LineIndex", "NoteLineLayer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionComboBreakItems_PlaySessionId_SongTimeMs",
                table: "PlaySessionComboBreakItems",
                columns: new[] { "PlaySessionId", "SongTimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionEnergyChangeItems_PlaySessionId_SongTimeMs",
                table: "PlaySessionEnergyChangeItems",
                columns: new[] { "PlaySessionId", "SongTimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionItemMotionFrames_PlaySessionId_SongTimeMs",
                table: "PlaySessionItemMotionFrames",
                columns: new[] { "PlaySessionId", "SongTimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionMotionSummaries_PlaySessionId",
                table: "PlaySessionMotionSummaries",
                column: "PlaySessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionNoteItems_PlaySessionId_SongTimeMs",
                table: "PlaySessionNoteItems",
                columns: new[] { "PlaySessionId", "SongTimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_BeatmapDifficultyId",
                table: "PlaySessions",
                column: "BeatmapDifficultyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_UserId",
                table: "PlaySessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionScoreChangeItems_PlaySessionId_SongTimeMs",
                table: "PlaySessionScoreChangeItems",
                columns: new[] { "PlaySessionId", "SongTimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_DeviceId",
                table: "RefreshTokens",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorSamples_ClientId",
                table: "SensorSamples",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SensorSamples_UserId_Metric_RecordedAt",
                table: "SensorSamples",
                columns: new[] { "UserId", "Metric", "RecordedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Handle",
                table: "Users",
                column: "Handle",
                unique: true,
                filter: "\"Handle\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeatmapDifficultyAnalyses");

            migrationBuilder.DropTable(
                name: "BeatSaverVersionDifficulties");

            migrationBuilder.DropTable(
                name: "MapLikes");

            migrationBuilder.DropTable(
                name: "MapListItems");

            migrationBuilder.DropTable(
                name: "PlayNoteAggregates");

            migrationBuilder.DropTable(
                name: "PlaySessionComboBreakItems");

            migrationBuilder.DropTable(
                name: "PlaySessionEnergyChangeItems");

            migrationBuilder.DropTable(
                name: "PlaySessionItemMotionFrames");

            migrationBuilder.DropTable(
                name: "PlaySessionMotionSummaries");

            migrationBuilder.DropTable(
                name: "PlaySessionNoteItems");

            migrationBuilder.DropTable(
                name: "PlaySessionScoreChangeItems");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "SensorSamples");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "BeatSaverVersions");

            migrationBuilder.DropTable(
                name: "MapLists");

            migrationBuilder.DropTable(
                name: "PlaySessions");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "HealthProxyClients");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "BeatSaverMaps");

            migrationBuilder.DropTable(
                name: "BeatmapDifficulties");

            migrationBuilder.DropTable(
                name: "BeatSaverUsers");

            migrationBuilder.DropTable(
                name: "Beatmaps");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
