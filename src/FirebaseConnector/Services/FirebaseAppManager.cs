using System.Collections.Concurrent;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace FirebaseConnector.Services;

public interface IFirebaseAppManager
{
    FirebaseMessaging? GetMessagingClient(string appId);
    bool IsMockMode(string appId);
    IEnumerable<string> GetConfiguredApps();
}

public class FirebaseAppManager : IFirebaseAppManager
{
    private readonly ConcurrentDictionary<string, FirebaseApp> _apps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _mockApps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<FirebaseAppManager> _logger;

    public FirebaseAppManager(IConfiguration configuration, ILogger<FirebaseAppManager> logger)
    {
        _logger = logger;
        InitializeConfiguredApps(configuration);
    }

    private void InitializeConfiguredApps(IConfiguration configuration)
    {
        var globalJson = configuration["FIREBASE_DEFAULT_CREDENTIALS_JSON"]
                      ?? configuration["FIREBASE_CREDENTIALS_JSON"] 
                      ?? configuration["FIREBASE_SECRET_JSON"] 
                      ?? Environment.GetEnvironmentVariable("FIREBASE_DEFAULT_CREDENTIALS_JSON")
                      ?? Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");

        var appKeys = new Dictionary<string, string>(); // AppId -> Raw JSON/Path

        // 1. Check Configuration sections (FirebaseProjects__<AppId>__CredentialsJson)
        var projectsSection = configuration.GetSection("FirebaseProjects");
        foreach (var project in projectsSection.GetChildren())
        {
            var appId = project.Key;
            var json = project["CredentialsJson"] ?? project.Value;
            var path = project["CredentialsPath"];
            if (!string.IsNullOrWhiteSpace(json))
            {
                appKeys[appId] = json;
            }
            else if (!string.IsNullOrWhiteSpace(path))
            {
                appKeys[appId] = path;
            }
        }

        // 2. Discover environment variables matching naming conventions:
        //    FIREBASE_APP_<NAME>_CREDENTIALS_JSON or FIREBASE_<NAME>_CREDENTIALS_JSON or FIREBASE_<NAME>_JSON
        var envVars = Environment.GetEnvironmentVariables();
        foreach (string key in envVars.Keys)
        {
            if (key.StartsWith("FIREBASE_", StringComparison.OrdinalIgnoreCase))
            {
                string? detectedApp = null;
                if (key.StartsWith("FIREBASE_APP_", StringComparison.OrdinalIgnoreCase) && key.EndsWith("_JSON", StringComparison.OrdinalIgnoreCase))
                {
                    // e.g. FIREBASE_APP_CUSTOMER_JSON or FIREBASE_APP_CUSTOMER_CREDENTIALS_JSON
                    detectedApp = key.Substring("FIREBASE_APP_".Length);
                    detectedApp = detectedApp.Replace("_CREDENTIALS_JSON", "", StringComparison.OrdinalIgnoreCase)
                                             .Replace("_JSON", "", StringComparison.OrdinalIgnoreCase);
                }
                else if (key.EndsWith("_CREDENTIALS_JSON", StringComparison.OrdinalIgnoreCase) && !key.Equals("FIREBASE_CREDENTIALS_JSON", StringComparison.OrdinalIgnoreCase) && !key.Equals("FIREBASE_DEFAULT_CREDENTIALS_JSON", StringComparison.OrdinalIgnoreCase))
                {
                    // e.g. FIREBASE_CUSTOMERAPP_CREDENTIALS_JSON -> CUSTOMERAPP
                    detectedApp = key.Substring("FIREBASE_".Length).Replace("_CREDENTIALS_JSON", "", StringComparison.OrdinalIgnoreCase);
                }
                else if (key.EndsWith("_JSON", StringComparison.OrdinalIgnoreCase) && !key.Equals("FIREBASE_SECRET_JSON", StringComparison.OrdinalIgnoreCase) && !key.Equals("FIREBASE_CREDENTIALS_JSON", StringComparison.OrdinalIgnoreCase))
                {
                    // e.g. FIREBASE_CUSTOMERAPP_JSON -> CUSTOMERAPP
                    detectedApp = key.Substring("FIREBASE_".Length).Replace("_JSON", "", StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrEmpty(detectedApp))
                {
                    var val = envVars[key]?.ToString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        appKeys[detectedApp] = val;
                    }
                }
            }
        }

        // If no specific apps discovered, but global JSON is provided, register standard apps
        if (!appKeys.Any())
        {
            if (!string.IsNullOrWhiteSpace(globalJson))
            {
                var globalCred = ParseCredential(globalJson);
                if (globalCred != null)
                {
                    var defaultApp = FirebaseApp.Create(new AppOptions { Credential = globalCred });
                    _apps.TryAdd("Default", defaultApp);
                    _apps.TryAdd("CustomerApp", defaultApp);
                    _apps.TryAdd("DriverApp", defaultApp);
                    _logger.LogInformation("✅ Initialized FirebaseApp instances for Default, CustomerApp, and DriverApp from global JSON secret.");
                    return;
                }
            }

            _logger.LogWarning("⚠️ No Firebase credentials or projects configured. Operating in Simulated/Mock mode.");
            _mockApps.Add("Default");
            _mockApps.Add("CustomerApp");
            _mockApps.Add("DriverApp");
            return;
        }

        foreach (var (appId, rawValue) in appKeys)
        {
            try
            {
                GoogleCredential? credential = null;

                if (File.Exists(rawValue))
                {
                    credential = GoogleCredential.FromFile(rawValue);
                    _logger.LogInformation("Loaded credentials for '{AppId}' from file: {Path}", appId, rawValue);
                }
                else
                {
                    credential = ParseCredential(rawValue);
                    if (credential != null)
                    {
                        _logger.LogInformation("✅ Loaded credentials for '{AppId}' from .env JSON secret.", appId);
                    }
                }

                if (credential != null)
                {
                    var app = FirebaseApp.Create(new AppOptions { Credential = credential }, appId);
                    _apps.TryAdd(appId, app);
                    _logger.LogInformation("✅ FirebaseApp initialized successfully for '{AppId}'.", appId);
                }
                else
                {
                    _logger.LogWarning("⚠️ Credentials invalid for '{AppId}'. Operating in Simulated/Mock mode.", appId);
                    _mockApps.Add(appId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize FirebaseApp for '{AppId}'. Falling back to Mock mode.", appId);
                _mockApps.Add(appId);
            }
        }
    }

    private GoogleCredential? ParseCredential(string input)
    {
        try
        {
            var trimmed = input.Trim().Trim('\'', '"');

            // If Base64 encoded, decode it
            if (!trimmed.StartsWith("{"))
            {
                try
                {
                    var bytes = Convert.FromBase64String(trimmed);
                    trimmed = System.Text.Encoding.UTF8.GetString(bytes);
                }
                catch { /* not base64, proceed as raw string */ }
            }

            return GoogleCredential.FromJson(trimmed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse GoogleCredential from provided JSON string.");
            return null;
        }
    }

    public FirebaseMessaging? GetMessagingClient(string appId)
    {
        if (_apps.TryGetValue(appId, out var app))
        {
            return FirebaseMessaging.GetMessaging(app);
        }

        // If only 1 real app is configured (e.g., FCMPOC), fallback to it
        if (_apps.Count == 1)
        {
            return FirebaseMessaging.GetMessaging(_apps.Values.First());
        }

        if (FirebaseApp.DefaultInstance != null)
        {
            return FirebaseMessaging.DefaultInstance;
        }

        return null; // Mock mode
    }

    public bool IsMockMode(string appId)
    {
        if (_apps.ContainsKey(appId)) return false;
        if (_apps.Count == 1) return false; // Single configured app covers all requests
        return _mockApps.Contains(appId) || _apps.IsEmpty;
    }

    public IEnumerable<string> GetConfiguredApps() => _apps.Keys.Concat(_mockApps).Distinct();
}
