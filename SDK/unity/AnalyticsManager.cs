using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private readonly string TENANT_ID = "sod";
    private readonly string URL = "https://vortex.dezeiraud.com";

#if !DISABLESTEAMWORKS
    private readonly string PLATFORM = "STEAM";
#else
   private string PLATFORM = "NODRM";
#endif

    private bool _serverAlive = true;

    [System.Serializable]
    public class TrackingData
    {
        public string name;
        public string value;
        public string identity;
        public string session_id;
        public string platform;
        public string app_version;
    }

    [System.Serializable]
    public class Tracking
    {
        public string tenant_id;
        public TrackingData tracking;
    }

    private string _identity = "";
    private string _sessionId = "";
    private string _appVersion = "";

    private void Start()
    {
#if ENABLE_ANALYTICS
            var identity = GetVortexString();
            if (string.IsNullOrEmpty(identity) == true)
            {
                identity = Guid.NewGuid().ToString();
                SetVortexString(identity);
            }
            _identity = identity;

            _sessionId = Guid.NewGuid().ToString();
            _appVersion = Application.version;

            Task.Run(async () => await CheckServerAvailabilityAsync()); // Check server status at startup
#endif
    }

    public void NewSessionId()
    {
        _sessionId = Guid.NewGuid().ToString();
    }

    private void SetVortexString(string value)
    {
        PlayerPrefs.SetString("device_identity", value);
    }

    private string GetVortexString()
    {
        return PlayerPrefs.GetString("device_identity", null);
    }

    private async Task CheckServerAvailabilityAsync()
    {
        try
        {
            using StringContent jsonContent = new(
                JsonUtility.ToJson(CreateTracking("", "")),
                Encoding.UTF8,
                "application/json");

            HttpClient httpClient = new HttpClient();
            var response = await httpClient.PostAsync(URL + "/valid", jsonContent);

            _serverAlive = response.IsSuccessStatusCode;
        }
        catch
        {
            Debug.LogError("Vortex Server is not reachable.");
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
                JsonUtility.ToJson(CreateTracking(name, value)),
                Encoding.UTF8,
                "application/json");

            HttpClient httpClient = new HttpClient();
            await httpClient.PostAsync(URL + "/track", jsonContent);
        }
        catch (Exception ex)
        {

        }
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
                var serializedProps = JsonUtility.ToJson(props);
                _ = PostAsync(eventName, serializedProps);
            });
#endif
    }
}