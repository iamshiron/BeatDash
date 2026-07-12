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
var builder = WebApplication.CreateBuilder(args);
var jwtSecret = builder.Configuration.GetSection("Jwt")["SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
var jwtIssuer = builder.Configuration.GetSection("Jwt")["Issuer"] ?? throw new InvalidOperationException("JWT issuer not configured");
var jwtAudience = builder.Configuration.GetSection("Jwt")["Audience"] ?? throw new InvalidOperationException("JWT audience not configured");

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
    options.Endpoint = builder.Configuration["MINIO_ENDPOINT"] ?? "localhost:9000";
    options.AccessKey = builder.Configuration["MINIO_ACCESS_KEY"] ?? "minioadmin";
    options.SecretKey = builder.Configuration["MINIO_SECRET_KEY"] ?? "minioadmin";
    options.UseSsl = bool.TryParse(builder.Configuration["MINIO_USE_SSL"], out var ssl) && ssl;
    options.BucketAssets = builder.Configuration["MINIO_BUCKET_ASSETS"] ?? "beatdash-assets";
    options.BucketUserData = builder.Configuration["MINIO_BUCKET_USER_DATA"] ?? "beatdash-user-data";
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
});

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
builder.Services.AddScoped<IWeaknessAggregationService, WeaknessAggregationService>();
builder.Services.AddHostedService<WeaknessBackfillService>();
builder.Services.AddScoped<IPracticeRecommendationService, PracticeRecommendationService>();
builder.Services.AddHostedService<MotionSummaryBackfillService>();
builder.Services.AddSingleton<IStorageService, MinioStorageService>();
builder.Services.AddScoped<IPinService, PinService>();
builder.Services.AddScoped<IBeatmapPersistenceService, BeatmapPersistenceService>();
builder.Services.AddHostedService<UdpSocketService>();

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
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

// MinIO
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
api.MapAdminMetricsEndpoints();
api.MapSessionEndpoints();
api.MapProfileEndpoints();
api.MapServerInfoEndpoints();
api.MapHub<RealtimeHub>("/client/web");

app.Run();
