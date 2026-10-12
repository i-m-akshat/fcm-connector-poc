using FirebaseConnector.Services;

// Auto-load .env file for local development if running outside containers
LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

static void LoadDotEnv()
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "../../.env"),
        Path.Combine(AppContext.BaseDirectory, ".env"),
        Path.Combine(AppContext.BaseDirectory, "../../../../.env")
    };

    foreach (var path in candidates)
    {
        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
                var idx = trimmed.IndexOf('=');
                if (idx > 0)
                {
                    var key = trimmed.Substring(0, idx).Trim();
                    var val = trimmed.Substring(idx + 1).Trim().Trim('\'', '"');
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, val);
                    }
                }
            }
            break;
        }
    }
}

// Add Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Firebase Connector API",
        Version = "v1",
        Description = "Enterprise Real-Time Notification Gateway with Active Presence and Multi-Tenant FCM Delivery."
    });
});

// Configure CORS for local browser simulator
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register Core Services as Singletons
builder.Services.AddSingleton<IFirebaseAppManager, FirebaseAppManager>();
builder.Services.AddSingleton<IPresenceService, PresenceService>();
builder.Services.AddSingleton<IDeviceRegistryService, DeviceRegistryService>();
builder.Services.AddScoped<INotificationDeliveryRouter, NotificationDeliveryRouter>();

var app = builder.Build();

// Enable Swagger in all environments for POC
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Firebase Connector API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseAuthorization();
// Serve simulator directly on http://localhost:5000/simulator/index.html
var candidates = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), "../../mobile-simulator"),
    Path.Combine(Directory.GetCurrentDirectory(), "mobile-simulator"),
    Path.Combine(AppContext.BaseDirectory, "mobile-simulator"),
    Path.Combine(AppContext.BaseDirectory, "../../../../mobile-simulator")
};
foreach (var simDir in candidates)
{
    if (Directory.Exists(simDir))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(simDir)),
            RequestPath = "/simulator"
        });
        break;
    }
}
app.MapGet("/simulator", () => Results.Redirect("/simulator/index.html"));

// Serve ADR & Architecture docs directly on http://localhost:5000/docs/index.html
var docCandidates = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), "../../Docs"),
    Path.Combine(Directory.GetCurrentDirectory(), "Docs"),
    Path.Combine(AppContext.BaseDirectory, "Docs"),
    Path.Combine(AppContext.BaseDirectory, "../../../../Docs")
};
foreach (var docDir in docCandidates)
{
    if (Directory.Exists(docDir))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(docDir)),
            RequestPath = "/docs"
        });
        break;
    }
}
app.MapGet("/docs", () => Results.Redirect("/docs/index.html"));
app.MapGet("/firebase-messaging-sw.js", async (HttpContext ctx) =>
{
    var swCandidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "mobile-simulator", "firebase-messaging-sw.js"),
        Path.Combine(AppContext.BaseDirectory, "mobile-simulator", "firebase-messaging-sw.js"),
        Path.Combine(Directory.GetCurrentDirectory(), "../../mobile-simulator", "firebase-messaging-sw.js")
    };
    foreach (var path in swCandidates)
    {
        if (File.Exists(path))
        {
            ctx.Response.ContentType = "application/javascript";
            await ctx.Response.SendFileAsync(path);
            return;
        }
    }
    ctx.Response.StatusCode = 404;
});
app.MapControllers();

// Health check endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "Firebase Connector",
    status = "Healthy",
    timeUtc = DateTime.UtcNow,
    swagger = "/swagger",
    simulator = "/simulator/index.html"
}));

app.Run();
