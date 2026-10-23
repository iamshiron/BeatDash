using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Quartz;
using Scalar.AspNetCore;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Seeders;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.API.Services.Impl;
using Shiron.BeatDash.API.Services.Motion;
using Shiron.BeatDash.Analysis;
using Shiron.BeatDash.API.Services.BeatmapAnalysis;
using Shiron.BeatDash.API.Services.BeatSaver;
using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.API.Services.Socket;
using Shiron.BeatDash.API.Services.Socket.Handlers;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

Env.TraversePath().Load();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    Args = args,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
});
var jwtSecret = builder.Configuration.GetSection("Jwt")["SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
var jwtIssuer = builder.Configuration.GetSection("Jwt")["Issuer"] ?? throw new InvalidOperationException("JWT issuer not configured");
var jwtAudience = builder.Configuration.GetSection("Jwt")["Audience"] ?? throw new InvalidOperationException("JWT audience not configured");
var backupOptions = builder.Configuration.GetSection("Backup").Get<BackupOptions>() ?? new BackupOptions();
var hspOptions = builder.Configuration.GetSection("Hsp").Get<HspOptions>() ?? new HspOptions();
_ = hspOptions.GetPublicIngestUri();
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection("Backup"));
builder.Services.Configure<HspOptions>(builder.Configuration.GetSection("Hsp"));
var backupDirectory = Path.GetFullPath(backupOptions.WorkDirectory);
var webDirectory = Path.GetFullPath(builder.Environment.WebRootPath!);
if (backupOptions.MaxArchiveBytes <= 0 || backupDirectory == webDirectory
    || backupDirectory.StartsWith(webDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Backup workspace must be private and the archive size limit must be positive.");
if (!backupOptions.MaintenanceMode && File.Exists(Path.Combine(backupOptions.WorkDirectory, "recovery.zip")))
    throw new InvalidOperationException("An interrupted restore requires backup maintenance mode and 'backup recover'.");

builder.Services.AddOpenApi();
builder.Services.AddIdentity<User, IdentityRole<Guid>>(c => {
    if (builder.Environment.IsDevelopment()) {
        c.Password.RequireDigit = false;
        c.Password.RequireLowercase = false;
        c.Password.RequireNonAlphanumeric = false;
        c.Password.RequireUppercase = false;
        c.Password.RequiredLength = 1;
    }

    c.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<BeatDashDbContext>();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Database connection string not configured");

builder.Services.AddDbContextFactory<BeatDashDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.Configure<StorageOptions>(options => {
    options.Endpoint = builder.Configuration["RUSTFS_ENDPOINT"] ?? builder.Configuration["MINIO_ENDPOINT"] ?? options.Endpoint;
    options.AccessKey = builder.Configuration["RUSTFS_ACCESS_KEY"] ?? builder.Configuration["MINIO_ACCESS_KEY"] ?? options.AccessKey;
    options.SecretKey = builder.Configuration["RUSTFS_SECRET_KEY"] ?? builder.Configuration["MINIO_SECRET_KEY"] ?? options.SecretKey;
    options.UseSsl = bool.TryParse(builder.Configuration["RUSTFS_USE_SSL"] ?? builder.Configuration["MINIO_USE_SSL"], out var ssl) && ssl;
    options.BucketAssets = builder.Configuration["RUSTFS_BUCKET_ASSETS"] ?? builder.Configuration["MINIO_BUCKET_ASSETS"] ?? options.BucketAssets;
    options.BucketUserData = builder.Configuration["RUSTFS_BUCKET_USER_DATA"] ?? builder.Configuration["MINIO_BUCKET_USER_DATA"] ?? options.BucketUserData;
});

builder.Services.Configure<UdpSocketOptions>(builder.Configuration.GetSection("UdpSocket"));
builder.Services.Configure<MotionFrameOptions>(builder.Configuration.GetSection("MotionFrame"));
builder.Services.Configure<BeatSaverOptions>(builder.Configuration.GetSection("BeatSaver"));

builder.Services.ConfigureApplicationCookie(o => {
    o.LoginPath = "/auth/login";
    o.Cookie.Name = "BeatDashAuth";
});

builder.Services.AddAuthentication(o => {
    o.DefaultScheme = IdentityConstants.ApplicationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o => {
    o.Events = new JwtBearerEvents {
        OnTokenValidated = context => {
            if (context.Principal?.FindFirst("purpose")?.Value == "beatdash-backup"
                && (!backupOptions.MaintenanceMode || !context.Request.Path.StartsWithSegments("/api/admin/backups")))
                context.Fail("Backup tokens are restricted to maintenance administration.");
            return Task.CompletedTask;
        }
    };
    o.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options => {
    var defaultAuthorizationPolicyBuilder = new AuthorizationPolicyBuilder(
        JwtBearerDefaults.AuthenticationScheme,
        IdentityConstants.ApplicationScheme);

    defaultAuthorizationPolicyBuilder = defaultAuthorizationPolicyBuilder.RequireAuthenticatedUser();
    options.DefaultPolicy = defaultAuthorizationPolicyBuilder.Build();
    options.AddPolicy("BackupAdmin", policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser().AddRequirements(new BackupAdminRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, BackupAdminHandler>();
builder.Services.AddSingleton<BackupWorkspace>();
builder.Services.AddScoped<BackupDatabase>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddSingleton<IBackupStorage, BackupStorage>();

builder.Services.AddMemoryCache();

// Services
builder.Services.AddSingleton<ITokenService>(new TokenService(jwtSecret, jwtIssuer, jwtAudience));
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddSingleton<IMapDataStore, MapDataStore>();
builder.Services.AddSingleton<IPlaySessionStore, PlaySessionStore>();
builder.Services.AddSingleton<IMotionFrameBuffer, MotionFrameBuffer>();
builder.Services.AddSingleton<IMotionFramePersistence, MotionFramePersistence>();
builder.Services.AddScoped<IPlaySessionService, PlaySessionService>();
builder.Services.AddScoped<IProfileStatsService, ProfileStatsService>();
builder.Services.AddScoped<Shiron.BeatDash.API.Services.Health.IHealthService, Shiron.BeatDash.API.Services.Health.HealthService>();
builder.Services.AddScoped<IWeaknessAggregationService, WeaknessAggregationService>();
if (!backupOptions.MaintenanceMode) builder.Services.AddHostedService<WeaknessBackfillService>();
builder.Services.AddScoped<IPracticeRecommendationService, PracticeRecommendationService>();
if (!backupOptions.MaintenanceMode) builder.Services.AddHostedService<MotionSummaryBackfillService>();
builder.Services.AddSingleton<IStorageService, MinioStorageService>();
builder.Services.AddScoped<IMapAudioService, MapAudioService>();
builder.Services.AddSingleton<IPinService, PinService>();
builder.Services.AddScoped<IBeatmapPersistenceService, BeatmapPersistenceService>();
if (!backupOptions.MaintenanceMode) builder.Services.AddHostedService<UdpSocketService>();

// BeatSaver fetch pipeline
builder.Services.AddSingleton(FeatureExtractor.CreateDefault());

// Metric scoring — calibration lives in the "Metrics" config section, overlaid on the
// provisional defaults, so it can be recalibrated without recompiling.
var metricConfig = MetricConfig.CreateDefault();
builder.Configuration.GetSection("Metrics").Bind(metricConfig);
builder.Services.AddSingleton(metricConfig);
builder.Services.AddSingleton(MetricScorer.CreateDefault(metricConfig));

builder.Services.AddScoped<IBeatmapAnalysisService, BeatmapAnalysisService>();
builder.Services.AddSingleton<BeatSaverRateLimiter>();
builder.Services.AddSingleton<IBeatSaverFetchTrigger, BeatSaverFetchTrigger>();
builder.Services.AddScoped<IBeatSaverFetchService, BeatSaverFetchService>();
builder.Services.AddHttpClient<IBeatSaverClient, BeatSaverClient>((sp, http) => {
    var o = sp.GetRequiredService<IOptions<BeatSaverOptions>>().Value;
    http.BaseAddress = new Uri(o.ApiBaseUrl);
    http.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
    http.Timeout = TimeSpan.FromSeconds(Math.Max(1, o.RequestTimeoutSeconds));
});

// Quartz scheduler + BeatSaver fetch job (startup + recurring sweeps)
var beatSaver = builder.Configuration.GetSection("BeatSaver").Get<BeatSaverOptions>() ?? new BeatSaverOptions();
builder.Services.AddQuartz(q => {
    q.AddJob<BeatSaverFetchJob>(o => o.WithIdentity(BeatSaverFetchJob.Key).StoreDurably());

    // Re-score maps whose metrics were computed under a now-stale calibration. Runs once
    // at startup; no-op unless the "Metrics" config changed since the last run.
    q.AddJob<BeatmapRescoreJob>(o => o.WithIdentity(BeatmapRescoreJob.Key).StoreDurably());
    q.AddTrigger(t => t
        .ForJob(BeatmapRescoreJob.Key)
        .WithIdentity("BeatmapRescore-startup")
        .StartNow());

    if (beatSaver.FetchOnStartup) {
        q.AddTrigger(t => t
            .ForJob(BeatSaverFetchJob.Key)
            .WithIdentity("BeatSaverFetch-startup")
            .StartNow());
    }

    if (beatSaver.ScheduledFetchEnabled) {
        var minutes = Math.Max(1, beatSaver.ScheduledFetchIntervalMinutes);
        q.AddTrigger(t => t
            .ForJob(BeatSaverFetchJob.Key)
            .WithIdentity("BeatSaverFetch-schedule")
            .StartAt(DateBuilder.FutureDate(minutes, IntervalUnit.Minute))
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(minutes)).RepeatForever()));
    }
});
if (!backupOptions.MaintenanceMode) builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

// RustFS uses the existing S3-compatible client, including backup streaming.
builder.Services.AddSingleton<IMinioClient>(sp => {
    var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    return new MinioClient()
        .WithEndpoint(opts.Endpoint)
        .WithCredentials(opts.AccessKey, opts.SecretKey)
        .WithSSL(opts.UseSsl)
        .Build();
});

// Socket dispatchers
builder.Services.AddScoped<SocketMessageDispatcher>();
builder.Services.AddScoped<SocketBinaryDispatcher>();

// Socket handlers
builder.Services.AddSocketBinaryHandler<MapStartHandler>(BinaryPacketTypes.MapStart);
builder.Services.AddSocketBinaryHandler<MapStateHandler>(BinaryPacketTypes.MapState);
builder.Services.AddSocketMessageHandler<LiveStatsMessage, LiveStatsHandler>();
builder.Services.AddSocketBinaryHandler<ScoreUpdateHandler>(BinaryPacketTypes.ScoreUpdate);
builder.Services.AddSocketBinaryHandler<MapCoverImageHandler>(BinaryPacketTypes.MapCoverImage);
builder.Services.AddSocketBinaryHandler<MotionFrameHandler>(BinaryPacketTypes.MotionFrameBatch);

// SignalR
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeBroadcaster, RealtimeBroadcaster>();

var app = builder.Build();
app.Use(async (context, next) => {
    var backupRequest = context.Request.Path.StartsWithSegments("/api/admin/backups");
    if (backupOptions.MaintenanceMode && !backupRequest && context.Request.Path != "/api/health") {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { Error = "BeatDash is in backup maintenance mode." });
        return;
    }
    if (backupRequest && !backupOptions.MaintenanceMode) {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return;
    }
    await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets(new WebSocketOptions {
    KeepAliveInterval = TimeSpan.FromSeconds(120)
});

using (var scope = app.Services.CreateScope()) {
    await scope.SeedAdminUser(
        app.Configuration.GetSection("AdminUser")["Email"] ?? throw new InvalidOperationException("Admin user email not configured"),
        app.Configuration.GetSection("AdminUser")["Password"] ?? throw new InvalidOperationException("Admin user password not configured"),
        app.Configuration.GetSection("AdminUser")["Role"] ?? throw new InvalidOperationException("Admin user role not configured")
    );
}

if (builder.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.MapScalarApiReference(o => {
        o.Title = "BeatDash API";
        o.Theme = ScalarTheme.Purple;
    });
}

app.UseHttpsRedirection();

var api = app.MapGroup("/api");
api.MapGet("/health", () => new { Message = "OK" }).WithTags("Health");
api.MapIdentityEndpoints();
api.MapDeviceEndpoints();
api.MapClientEndpoints();
api.MapMapEndpoints();
api.MapMapLikeEndpoints();
api.MapMapListEndpoints();
api.MapAdminMetricsEndpoints();
api.MapBackupEndpoints();
api.MapSessionEndpoints();
api.MapProfileEndpoints();
api.MapUserAssetEndpoints();
api.MapHealthEndpoints();
api.MapHspEndpoints();
api.MapServerInfoEndpoints();
api.MapHub<RealtimeHub>("/client/web");

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
