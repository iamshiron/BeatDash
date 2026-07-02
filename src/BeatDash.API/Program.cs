using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Scalar.AspNetCore;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Seeders;
using Shiron.BeatDash.API.Services;
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

builder.Services.AddDbContext<BeatDashDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Database connection string not configured")
    ));

builder.Services.Configure<StorageOptions>(options => {
    options.Endpoint = builder.Configuration["MINIO_ENDPOINT"] ?? "localhost:9000";
    options.AccessKey = builder.Configuration["MINIO_ACCESS_KEY"] ?? "minioadmin";
    options.SecretKey = builder.Configuration["MINIO_SECRET_KEY"] ?? "minioadmin";
    options.UseSsl = bool.TryParse(builder.Configuration["MINIO_USE_SSL"], out var ssl) && ssl;
    options.BucketAssets = builder.Configuration["MINIO_BUCKET_ASSETS"] ?? "beatdash-assets";
    options.BucketUserData = builder.Configuration["MINIO_BUCKET_USER_DATA"] ?? "beatdash-user-data";
});

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
builder.Services.AddScoped<IPinService, PinService>();

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
builder.Services.AddSocketMessageHandler<MapStartMessage, MapStartHandler>();
builder.Services.AddSocketBinaryHandler<MapCoverImageHandler>(BinaryPacketTypes.MapCoverImage);

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

app.Run();
