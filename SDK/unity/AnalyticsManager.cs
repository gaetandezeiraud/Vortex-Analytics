using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AnalyticsManager : MonoBehaviour
{
    [System.Serializable]
    public class ValueWrapper { public string data; }

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

    public static AnalyticsManager Instance { get; private set; }

    [SerializeField] private bool _initializeOnAwake = true;

    [SerializeField] private string _tenantId;
    [SerializeField] private string _url;
    [SerializeField] private string _platform;

    [Header("Auto-Flush Settings (Doesn't consern the manual batching API)")]
    [Tooltip("If true, all events are queued until a manual Flush or timer Flush occurs.")]
    [SerializeField] private bool _autoBatching = false;
    [SerializeField] private float _autoFlushInterval = 10f;

    private string _identity;
    private string _sessionId;
    private string _appVersion;

    private bool _isServerChecked;
    private bool _serverAlive;
    private bool _initialized;

    private readonly List<Tracking> _internalQueue = new List<Tracking>();
    private BatchedTracks _manualBatchedTracks = new BatchedTracks();

    private readonly object _lock = new object();
    private Coroutine _autoFlushRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_initializeOnAwake)
            Initialize();
    }

    public void Init(string tenantId, string url, string platform)
    {
        if (_initialized) return;

        _tenantId = tenantId;
        _url = url;
        _platform = platform;

        Initialize();
    }

    private void Initialize()
    {
        if (_initialized) return;

        _initialized = true;
        InitSession();

#if ENABLE_ANALYTICS
        StartCoroutine(CheckServerAvailability());
#endif

        TrackEvent("app_started");
    }

    private void InitSession()
    {
        _identity = PlayerPrefs.GetString("device_identity", Guid.NewGuid().ToString());
        PlayerPrefs.SetString("device_identity", _identity);
        _sessionId = Guid.NewGuid().ToString();
        _appVersion = Application.version;
    }

    private Tracking CreateTracking(string name, string value)
    {
        return new Tracking
        {
            tenant_id = _tenantId,
            tracking = new TrackingData
            {
                name = name,
                value = value,
                identity = _identity,
                session_id = _sessionId,
                platform = _platform,
                app_version = _appVersion
            }
        };
    }

    // Networking
    private IEnumerator CheckServerAvailability()
    {
        if (string.IsNullOrEmpty(_url)) yield break;

        using UnityWebRequest request = UnityWebRequest.Get(_url + "/health");
        request.timeout = 5;

        yield return request.SendWebRequest();

        _serverAlive = request.result == UnityWebRequest.Result.Success;
        _isServerChecked = true;

        if (_serverAlive)
        {
            if (_autoBatching && _autoFlushRoutine == null)
                _autoFlushRoutine = StartCoroutine(AutoFlushRoutine());

            if (!_autoBatching && _internalQueue.Count > 0)
                StartCoroutine(FlushInternalQueue());
        }
        else
        {
            if (_autoFlushRoutine != null)
            {
                StopCoroutine(_autoFlushRoutine);
                _autoFlushRoutine = null;
            }
        }
    }

    private UnityWebRequest CreateRequest(string url, string json)
    {
        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    // Sending
    private IEnumerator PostSingle(Tracking tracking)
    {
        string json = JsonUtility.ToJson(tracking);
        using UnityWebRequest request = CreateRequest(_url + "/track", json);
        yield return request.SendWebRequest();
    }

    public void FlushManualBatch() 
    {
#if ENABLE_ANALYTICS
        StartCoroutine(PostBatchRoutine());
#endif
    }

    private IEnumerator PostBatchRoutine()
    {
        if (!_serverAlive || _manualBatchedTracks.tracks.Count == 0) yield break;

        string json;
        lock (_lock)
        {
            json = JsonUtility.ToJson(_manualBatchedTracks);
            _manualBatchedTracks.tracks.Clear();
        }

        using UnityWebRequest request = CreateRequest(_url + "/batch", json);
        yield return request.SendWebRequest();
    }

    private IEnumerator FlushInternalQueue()
    {
        if (_internalQueue.Count == 0) yield break;

        List<Tracking> toSend;
        lock (_lock)
        {
            toSend = new List<Tracking>(_internalQueue);
            _internalQueue.Clear();
        }

        BatchedTracks batch = new BatchedTracks { tracks = toSend };
        string json = JsonUtility.ToJson(batch);

        using UnityWebRequest request = CreateRequest(_url + "/batch", json);
        yield return request.SendWebRequest();
    }

    private IEnumerator AutoFlushRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_autoFlushInterval);
            
            if (_serverAlive && _internalQueue.Count > 0)
                yield return FlushInternalQueue();
        }
    }

    // Public methods
    public void TrackEvent(string eventName, Dictionary<string, object> props)
    {
#if ENABLE_ANALYTICS
        if (!_serverAlive && _isServerChecked && !_autoBatching)
            return;

        string serializedProps = JsonUtility.ToJson(props); 
        ProcessTrackEvent(eventName, serializedProps);
#endif
    }

    public void TrackEvent(string eventName, string props = "")
    {
#if ENABLE_ANALYTICS
        if (!_serverAlive && _isServerChecked && !_autoBatching)
            return;

        string wrapped = string.IsNullOrEmpty(props) ? "" : JsonUtility.ToJson(new ValueWrapper { data = props });
        ProcessTrackEvent(eventName, wrapped);
#endif
    }

    private void ProcessTrackEvent(string eventName, string value)
    {
        Tracking tracking = CreateTracking(eventName, value);
        lock (_lock)
        {
            if (!_isServerChecked || _autoBatching)
                _internalQueue.Add(tracking);
            else
                StartCoroutine(PostSingle(tracking));
        }
    }

    public void BatchedTrackEvent(string eventName, Dictionary<string, object> props)
    {
#if ENABLE_ANALYTICS
        if (!_serverAlive) return;
        string serializedProps = JsonUtility.ToJson(props);
        lock (_lock) { _manualBatchedTracks.tracks.Add(CreateTracking(eventName, serializedProps)); }
#endif
    }

    public void BatchedTrackEvent(string eventName, string props = "")
    {
#if ENABLE_ANALYTICS
        if (!_serverAlive) return;
        lock (_lock) { _manualBatchedTracks.tracks.Add(CreateTracking(eventName, props)); }
#endif
    }

    // Lifecycle
    private void OnApplicationQuit()
    {
        FlushAllBeforeClosing();
    }

    // Useful for mobile when the app is pushed to the background
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            FlushAllBeforeClosing();
        }
    }

    private void FlushAllBeforeClosing()
    {
        lock (_lock)
        {
            if (_internalQueue.Count > 0)
            {
                _manualBatchedTracks.tracks.AddRange(_internalQueue);
                _internalQueue.Clear();
            }
        }

        if (_manualBatchedTracks.tracks.Count == 0) return;
        StartCoroutine(PostBatchRoutine());
        
        Debug.Log("[Analytics] Attempting final flush before exit...");
    }
}