using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class AnalyticsManager : Singleton<AnalyticsManager>
{
    private readonly string TENANT_ID = "ID";
    private readonly string URL = "https://vortex.dezeiraud.com";
    private string PLATFORM = "PLATFORM";

    private bool _serverAlive = true;

    public struct TrackingData
    {
        public string name { get; set; }
        public string value { get; set; }
        public string identity { get; set; }
        public string session_id { get; set; }
        public string platform { get; set; }
        public string app_version { get; set; }
    }

    public struct Tracking
    {
        public string tenant_id { get; set; }
        public TrackingData tracking { get; set; }
    }

    public struct BatchedTracks
    {
        public List<Tracking> tracks { get; set; } = new List<Tracking>();

        public BatchedTracks() { }
    }

    private string _identity = "";
    private string _sessionId = "";
    private string _appVersion = "";

    private readonly string VortexIdentityFilePath
        = $"{Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow"))}/COMPANY/GAME/vortex.id";

    private BatchedTracks _batchedTracks = new BatchedTracks();

    public AnalyticsManager()
    {
#if ENABLE_ANALYTICS
        var identity = GetString();
        if (string.IsNullOrEmpty(identity) == true)
        {
            identity = Guid.NewGuid().ToString();
            SetString(identity);
        }
        _identity = identity;

        _sessionId = Guid.NewGuid().ToString();
        _appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

        Task.Run(async () => await CheckServerAvailabilityAsync()); // Check server status at startup
#endif
    }

    public void NewSessionId()
    {
        _sessionId = Guid.NewGuid().ToString();
    }

    private void SetString(string value)
    {
        EnsureDirectoyExists();
        File.WriteAllText(VortexIdentityFilePath, value);
    }

    private string GetString()
    {
        if (File.Exists(VortexIdentityFilePath))
            return File.ReadAllText(VortexIdentityFilePath);
        else
            return string.Empty;
    }

    private void EnsureDirectoyExists()
    {
        string directoryPath = Path.GetDirectoryName(VortexIdentityFilePath);
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);
    }

    private async Task CheckServerAvailabilityAsync()
    {
        try
        {
            using StringContent jsonContent = new(
            JsonSerializer.Serialize(CreateTracking("", "")),
            Encoding.UTF8,
            "application/json");

            HttpClient httpClient = new HttpClient();
            var response = await httpClient.PostAsync(URL + "/valid", jsonContent);

            _serverAlive = response.IsSuccessStatusCode;
        }
        catch
        {
            _serverAlive = false;
        }
    }


    private Tracking CreateTracking(string name, string? value)
    {
        Tracking tracking = new Tracking();
        tracking.tenant_id = TENANT_ID;

        TrackingData trackingData = new TrackingData();
        trackingData.name = name;
        trackingData.value = value;
        trackingData.identity = _identity;
        trackingData.session_id = _sessionId;
        trackingData.platform = PLATFORM;
        trackingData.app_version = _appVersion;

        tracking.tracking = trackingData;
        return tracking;
    }

    private async Task PostAsync(string name, string? value)
    {
        try
        {
            using StringContent jsonContent = new(
                JsonSerializer.Serialize(CreateTracking(name, value)),
                Encoding.UTF8,
                "application/json");

            HttpClient httpClient = new HttpClient();
            await httpClient.PostAsync(URL + "/track", jsonContent);
        }
        catch (Exception ex)
        {

        }
    }

    public async Task PostBatchAsync()
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
        try
        {
            using StringContent jsonContent = new(
                JsonSerializer.Serialize(_batchedTracks),
                Encoding.UTF8,
                "application/json");
            _batchedTracks.tracks.Clear();

            HttpClient httpClient = new HttpClient();
            await httpClient.PostAsync(URL + "/batch", jsonContent);
        }
        catch (Exception ex)
        {

        }
#endif
    }

    public void TrackEvent(string eventName)
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
        _ = PostAsync(eventName, null);
#endif
    }

    public void TrackEvent(string eventName, string props)
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
        _ = PostAsync(eventName, props);
#endif
    }

    public void TrackEvent(string eventName, Dictionary<string, object>? props)
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
        Task.Run(() =>
        {
            var serializedProps = JsonSerializer.Serialize(props);
            _ = PostAsync(eventName, serializedProps);
        });
#endif
    }

    public void BatchedTrackEvent(string eventName)
    {
        if (!_serverAlive)
            return;

        _batchedTracks.tracks.Add(CreateTracking(eventName, null));
    }

    public void BatchedTrackEvent(string eventName, string props)
    {
        if (!_serverAlive)
            return;

        _batchedTracks.tracks.Add(CreateTracking(eventName, props));
    }

    public void BatchedTrackEvent(string eventName, Dictionary<string, object>? props)
    {
        if (!_serverAlive)
            return;

        Task.Run(() =>
        {
            var serializedProps = JsonSerializer.Serialize(props);
            _batchedTracks.tracks.Add(CreateTracking(eventName, serializedProps));
        });
    }
}