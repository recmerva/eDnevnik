using System.Threading.RateLimiting;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
// Frontend i API su na Render-u na različitim domenima, pa kolačići moraju biti SameSite=None + Secure.
var cookieSameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
var cookieSecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

builder.Services.AddDbContext<SchoolDbContext>(options =>
    options.UseMySql(BuildConnectionString(builder.Configuration), new MySqlServerVersion(new Version(8, 4, 0))));
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<ActivityService>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<SchoolOptions>(builder.Configuration.GetSection("School"));

builder.Services.AddControllers(options => options.Filters.Add<AntiforgeryValidationFilter>());
// Greške validacije vraćamo kao { message } da bi ih frontend prikazao direktno korisniku.
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var message = context.ModelState.Values.SelectMany(entry => entry.Errors)
        .Select(error => error.ErrorMessage)
        .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? "Podaci nisu ispravni.";
    return new BadRequestObjectResult(new { message });
});
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = cookieSameSite;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "mojdnevnik.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = cookieSameSite;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        // Ako je nalog obrisan ili mu je promenjena uloga, stari kolačić više ne važi.
        options.Events.OnValidatePrincipal = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<SchoolDbContext>();
            var userId = context.Principal?.GetUserId() ?? 0;
            var role = await db.Users.Where(user => user.Id == userId).Select(user => user.Role).SingleOrDefaultAsync();
            if (role is null || !context.Principal!.IsInRole(role))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) => new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
        new { message = "Previše pokušaja. Sačekajte minut pa pokušajte ponovo." }, cancellationToken));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
var frontendOrigins = builder.Configuration.GetSection("FrontendOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(frontendOrigins).AllowCredentials().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await DatabaseSeeder.InitializeAsync(app.Services, app.Configuration);

app.UseForwardedHeaders();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { message = "Došlo je do greške na serveru. Pokušajte ponovo." });
}));
if (isDevelopment) app.MapOpenApi();

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/auth/csrf", (IAntiforgery antiforgery, HttpContext context) =>
    Results.Ok(new { requestToken = antiforgery.GetAndStoreTokens(context).RequestToken }));
app.MapGet("/api/health", async (SchoolDbContext db, CancellationToken cancellationToken) =>
{
    var connected = await db.Database.CanConnectAsync(cancellationToken);
    return Results.Ok(new { api = "ok", database = connected ? "connected" : "unavailable" });
});
app.MapControllers();

app.Run();

static string BuildConnectionString(IConfiguration configuration)
{
    if (string.IsNullOrWhiteSpace(configuration["MYSQL_HOST"]))
    {
        return configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySQL konekcija nije podešena.");
    }

    return new MySqlConnectionStringBuilder
    {
        Server = configuration["MYSQL_HOST"],
        Port = uint.TryParse(configuration["MYSQL_PORT"], out var port) ? port : 3306,
        Database = configuration["MYSQL_DATABASE"],
        UserID = configuration["MYSQL_USER"],
        Password = configuration["MYSQL_PASSWORD"],
    }.ConnectionString;
}
