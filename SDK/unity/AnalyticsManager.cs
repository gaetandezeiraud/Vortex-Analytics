using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class AnalyticsManager : MonoBehaviour
{
    [System.Serializable]
    public class ValueWrapper
    {
        public string data;
    }

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

    [System.Serializable]
    public class BatchedTracks
    {
        public List<Tracking> tracks = new List<Tracking>();
    }

    // Variables
    public static AnalyticsManager Instance { get; private set; }

    [SerializeField] private string _tenantId;
    [SerializeField] private string _url;
    [SerializeField] private string _platform;
    private string _identity;
    private string _sessionId;
    private string _appVersion;

    private bool _serverAlive = true;
    private BatchedTracks _batchedTracks = new BatchedTracks();

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

    public void Init(string tenantId, string url, string platform)
    {
        _tenantId = tenantId;
        _url = url;
        _platform = platform;
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
            var response = await httpClient.PostAsync(_url + "/health", jsonContent);

            _serverAlive = response.IsSuccessStatusCode;
        }
        catch
        {
            Debug.LogError("Vortex Server is not reachable.");
            _serverAlive = false;
        }
    }


    private Tracking CreateTracking(string name, string value)
    {
        Tracking tracking = new Tracking();
        tracking.tenant_id = _tenantId;

        TrackingData trackingData = new TrackingData();
        trackingData.name = name;
        trackingData.value = value;
        trackingData.identity = _identity;
        trackingData.session_id = _sessionId;
        trackingData.platform = _platform;
        trackingData.app_version = _appVersion;

        tracking.tracking = trackingData;
        return tracking;
    }

    private async Task PostAsync(string name, string value)
    {
        try
        {
            string json = JsonUtility.ToJson(CreateTracking(name, value));

            using StringContent jsonContent = new(json, Encoding.UTF8, "application/json");

            HttpClient httpClient = new HttpClient();
            await httpClient.PostAsync(_url + "/track", jsonContent);
        }
        catch (Exception ex)
        {
            Debug.LogError("Analytics error: " + ex.Message);
        }
    }

    public async Task PostBatchAsync()
    {
        if (!_serverAlive)
            return;

        try
        {
            using StringContent jsonContent = new(
                JsonUtility.ToJson(_batchedTracks),
                Encoding.UTF8,
                "application/json");
            _batchedTracks.tracks.Clear();

            HttpClient httpClient = new HttpClient();
            await httpClient.PostAsync(_url + "/batch", jsonContent);
        }
        catch (Exception ex)
        {
            Debug.LogError("Analytics error: " + ex.Message);
        }
    }

    public void TrackEvent(string eventName)
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
            _ = PostAsync(eventName, "");
#endif
    }

    public void TrackEvent(string eventName, string props)
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
            string wrapped = JsonUtility.ToJson(new ValueWrapper { data = props });
            _ = PostAsync(eventName, wrapped);
#endif
    }

    public void TrackEvent(string eventName, Dictionary<string, object> props)
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

    
    public void BatchedTrackEvent(string eventName)
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
        _batchedTracks.tracks.Add(CreateTracking(eventName, ""));
#endif
    }

    public void BatchedTrackEvent(string eventName, string props)
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
        string wrapped = JsonUtility.ToJson(new ValueWrapper { data = props });
        _batchedTracks.tracks.Add(CreateTracking(eventName, wrapped));
#endif
    }

    public void BatchedTrackEvent(string eventName, Dictionary<string, object> props)
    {
        if (!_serverAlive)
            return;

#if ENABLE_ANALYTICS
        Task.Run(() =>
        {
            var serializedProps = JsonUtility.ToJson(props);
            _batchedTracks.tracks.Add(CreateTracking(eventName, serializedProps));
        });
#endif
    }
}