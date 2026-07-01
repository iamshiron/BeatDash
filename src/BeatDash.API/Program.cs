using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Seeders;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

Env.TraversePath().Load();
var builder = WebApplication.CreateBuilder(args);
var jwtSecret = builder.Configuration.GetSection("Jwt")["SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
var jwtIssuer = builder.Configuration.GetSection("Jwt")["Issuer"] ?? throw new InvalidOperationException("JWT issuer not configured");
var jwtAudience = builder.Configuration.GetSection("Jwt")["Audience"] ?? throw new InvalidOperationException("JWT audience not configured");

builder.Services.AddSingleton<ITokenService, TokenService>();

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

builder.Services.ConfigureApplicationCookie(o => {
    o.LoginPath = "/auth/login";
    o.Cookie.Name = "BeatDashAuth";
});

builder.Services.AddAuthentication(o => {
    o.DefaultScheme = IdentityConstants.ApplicationScheme;
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

builder.Services.AddAuthorization();

var app = builder.Build();
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
app.MapBeatSaberEndpoints();
api.MapIdentityEndpoints();

app.Run();
