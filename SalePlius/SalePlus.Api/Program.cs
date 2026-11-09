using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SalePlus.Api.Services;
using SalePlius.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Secrets are never stored in the repository. They come from configuration:
//    - on a developer PC: user-secrets (dotnet user-secrets set "Jwt:Key" "...", "Firebase:CredentialsPath" "...")
//    - when hosted: environment variables (Jwt__Key, Firebase__CredentialsJson or Firebase__CredentialsPath)

// 2. Controllers with JSON + XML content negotiation
builder.Services.AddControllers(options =>
{
    // Adds "_links" (hypermedia) to every program/workout/exercise/user/report the API returns.
    options.Filters.Add<HypermediaFilter>();
    options.RespectBrowserAcceptHeader = true;
    options.ReturnHttpNotAcceptable = false;
})
.AddXmlSerializerFormatters();

builder.Services.AddProblemDetails();

// 3. CORS: restrict to configured client origins; open only in Development as a fallback
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Client", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
    });
});

// 4. Authentication & Authorization (Hierarchical roles)
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SalePliusApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SalePliusClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name,
        // Access tokens live 15 minutes; the default 5-minute grace period would add a third to that.
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    // On every request: a token ended by logout is rejected, a deleted account is rejected,
    // and role changes apply immediately.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var service = ctx.HttpContext.RequestServices.GetRequiredService<IFirestoreService>();

            var jti = ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
            if (!string.IsNullOrEmpty(jti) && await service.IsAccessTokenRevokedAsync(jti))
            {
                ctx.Fail("Žetonas atšauktas (vartotojas atsijungė).");
                return;
            }

            var user = string.IsNullOrEmpty(userId) ? null : await service.GetUserByIdAsync(userId);
            if (user == null)
            {
                ctx.Fail("Vartotojo paskyra nebeegzistuoja.");
                return;
            }

            if (ctx.Principal!.Identity is ClaimsIdentity identity)
            {
                foreach (var old in identity.FindAll(ClaimTypes.Role).ToList())
                {
                    identity.RemoveClaim(old);
                }
                identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));
            }
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    // Hierarchy: Admin role inherits all User role permissions
    options.AddPolicy("RequireUser", policy =>
        policy.RequireRole(AppRoles.User, AppRoles.Admin));
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole(AppRoles.Admin));
});

builder.Services.AddScoped<ITokenService, TokenService>();

// 5. OpenAPI
builder.Services.AddOpenApi();

// 6. Firestore. The service-account key comes from configuration: the whole JSON (handy for hosting) or a
//    path to the file. With neither, Google's default credentials are used (e.g. the identity of a Cloud Run service).
var firebase = builder.Configuration.GetSection("Firebase");
var credentialsJson = firebase["CredentialsJson"];
var credentialsPath = firebase["CredentialsPath"];
// Loaded explicitly as a service-account key, so no other kind of credential can be slipped in.
GoogleCredential? firestoreCredential =
    !string.IsNullOrWhiteSpace(credentialsJson) ? CredentialFactory.FromJson<ServiceAccountCredential>(credentialsJson).ToGoogleCredential()
    : !string.IsNullOrWhiteSpace(credentialsPath) ? CredentialFactory.FromFile<ServiceAccountCredential>(credentialsPath).ToGoogleCredential()
    : null;
builder.Services.AddSingleton(_ => new FirestoreDbBuilder
{
    ProjectId = firebase["ProjectId"] ?? "saleplius",
    Credential = firestoreCredential,
}.Build());

// 7. Login, registration and token refresh: at most 60 attempts per minute from one IP address,
//    which makes guessing passwords impractically slow. Extra attempts get 429 Too Many Requests.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, token) =>
        new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Per daug bandymų. Pabandykite po minutės." }, token));
});
builder.Services.AddScoped<IFirestoreService, FirestoreService>();

// 8. Hosting behind a proxy (e.g. Render): the proxy handles HTTPS and forwards requests over HTTP.
//    Its X-Forwarded-Proto / X-Forwarded-For headers tell the app the real scheme (so hypermedia links
//    start with https://) and the visitor's real IP (so the login limit counts each visitor separately).
//    Only the last entry, added by the proxy itself, is used, so a visitor cannot fake their IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();   // the proxy's address is not fixed on the host
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
}

// 9. Seed initial data (service logs its own errors)
using (var scope = app.Services.CreateScope())
{
    var service = scope.ServiceProvider.GetRequiredService<IFirestoreService>();
    await service.SeedInitialDataAsync();
}

// 10. Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet("/", () => Results.Redirect("/scalar/v1"));
    app.MapGet("/swagger", () => Results.Redirect("/scalar/v1"));

    // Connection test: Development only
    app.MapGet("/firebase-test", async (FirestoreDb db) =>
    {
        var document = db.Collection("_connectionTests").Document("saleplius");
        await document.SetAsync(new { connected = true, testedAt = DateTime.UtcNow });
        var snapshot = await document.GetSnapshotAsync();
        return Results.Ok(snapshot.ToDictionary());
    });
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

// Health check for the host (Render: Health Check Path = /health)
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors("Client");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
