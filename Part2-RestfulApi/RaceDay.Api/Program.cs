using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Controllers + JSON ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection("AzureStorage"));

// The "Testing" environment is used by the unit tests, which register an in-memory database
// and an in-memory blob store instead.
if (!builder.Environment.IsEnvironment("Testing"))
{
    // ---------- Database (EF Core Code-First, SQL Server) ----------
    builder.Services.AddDbContext<RaceDayDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // ---------- Azure Blob Storage ----------
    var storageConnection = builder.Configuration["AzureStorage:ConnectionString"];
    if (string.IsNullOrWhiteSpace(storageConnection))
        builder.Services.AddSingleton<IBlobStorageService, UnconfiguredBlobStorageService>();
    else
        builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
}

// ---------- Session authentication (server-issued, encrypted session cookie) ----------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RaceDay.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;

        // This is an API: return status codes instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "RESTful API for the RaceDay Event Management System. " +
                      "Register or log in first (POST /api/auth/register or /api/auth/login); " +
                      "the browser then keeps your session cookie so you can test protected endpoints."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ---------- Create / migrate the database and seed (retries while SQL Server starts, e.g. in Docker) ----------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordService>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            if (db.Database.IsRelational() && db.Database.GetMigrations().Any())
                db.Database.Migrate();
            else
                db.Database.EnsureCreated();

            // Part 3 added two nullable image columns. If the database was created by Part 2, add them in place.
            if (db.Database.IsSqlServer())
            {
                db.Database.ExecuteSqlRaw("IF COL_LENGTH('dbo.Event', 'BannerBlobName') IS NULL ALTER TABLE dbo.Event ADD BannerBlobName NVARCHAR(200) NULL;");
                db.Database.ExecuteSqlRaw("IF COL_LENGTH('dbo.Participant', 'ProfilePictureBlobName') IS NULL ALTER TABLE dbo.Participant ADD ProfilePictureBlobName NVARCHAR(200) NULL;");
            }

            if (app.Configuration.GetValue("SeedData", true))
                DbSeeder.Seed(db, hasher);

            break;
        }
        catch (Exception ex) when (attempt < 45 && !app.Environment.IsEnvironment("Testing"))
        {
            logger.LogWarning(ex, "Database not ready (attempt {Attempt}/45). Retrying in 2 seconds...", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1"));

if (app.Configuration.GetValue("UseHttpsRedirection", false) && !app.Environment.IsEnvironment("Testing"))
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

app.Run();

// Makes the Program class visible to the test project (WebApplicationFactory<Program>).
public partial class Program { }
