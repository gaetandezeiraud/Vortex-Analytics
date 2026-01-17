using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class AnalyticsManager
{
    // Data structures
    public class ValueWrapper { public string data { get; set; } }

    public class TrackingData
    {
        public string name { get; set; }
        public string value { get; set; }
        public string identity { get; set; }
        public string session_id { get; set; }
        public string platform { get; set; }
        public string app_version { get; set; }
        public string timestamp { get; set; }
    }

    public class Tracking
    {
        public string tenant_id { get; set; }
        public TrackingData tracking { get; set; }
    }

    public class BatchedTracks
    {
        public List<Tracking> tracks { get; set; } = new List<Tracking>();
    }

    // Singleton
    private static readonly Lazy<AnalyticsManager> _lazy = new Lazy<AnalyticsManager>(() => new AnalyticsManager());
    public static AnalyticsManager Instance => _lazy.Value;

    // Settings
    private string _tenantId;
    private string _url = "https://in.vortexanalytics.io";
    private string _platform;
    private bool _autoBatching = false;
    private int _autoFlushIntervalMs = 10000;

    // State
    private string _identity;
    private string _sessionId;
    private string _appVersion;
    private bool _initialized;
    private bool _serverAlive;
    private bool _isServerChecked;

    private readonly List<Tracking> _internalQueue = new List<Tracking>();
    private readonly BatchedTracks _manualBatchedTracks = new BatchedTracks();
    
    private readonly object _lock = new object();
    private static readonly HttpClient _httpClient = new HttpClient();
    private CancellationTokenSource _cts;

    private AnalyticsManager() { }

    // Setup methods
    public void Init(string tenantId, string url, string platform, string appVersion = "1.0.0", bool autoBatching = false, int flushIntervalSec = 10)
    {
        if (_initialized) return;

        _tenantId = tenantId;
        _url = url.TrimEnd('/');
        _platform = platform;
        _appVersion = appVersion;
        _autoBatching = autoBatching;
        _autoFlushIntervalMs = flushIntervalSec * 1000;

        Initialize();
    }

    private void Initialize()
    {
        _initialized = true;
        InitSession();

        // Run server check in background
        Task.Run(CheckServerAvailabilityAsync);

        TrackEvent("app_started");
    }

    private void InitSession()
    {
        _identity = GetPersistentIdentity();
        _sessionId = Guid.NewGuid().ToString();
    }

    private string GetPersistentIdentity()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analytics.id");
        try
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            
            string newId = Guid.NewGuid().ToString();
            File.WriteAllText(path, newId);
            return newId;
        }
        catch 
        { 
            return Guid.NewGuid().ToString(); 
        }
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
                app_version = _appVersion,
                timestamp = DateTime.UtcNow.ToString("o")
            }
        };
    }

    // Networking
    private async Task CheckServerAvailabilityAsync()
    {
        if (string.IsNullOrEmpty(_url)) return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync($"{_url}/health", cts.Token);
            _serverAlive = response.IsSuccessStatusCode;
        }
        catch
        {
            _serverAlive = false;
        }

        _isServerChecked = true;

        if (_serverAlive)
        {
            if (_autoBatching) StartAutoFlush();
            else await FlushInternalQueueAsync();
        }
    }

    private async Task<bool> SendRequestAsync(string endpoint, object data)
    {
        try
        {
            string json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_url}{endpoint}", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Flush logic
    private void StartAutoFlush()
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () => 
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(_autoFlushIntervalMs, _cts.Token);
                if (_serverAlive) await FlushInternalQueueAsync();
            }
        }, _cts.Token);
    }

    private async Task FlushInternalQueueAsync()
    {
        List<Tracking> toSend;
        lock (_lock)
        {
            if (_internalQueue.Count == 0) return;
            toSend = new List<Tracking>(_internalQueue);
            _internalQueue.Clear();
        }

        var batch = new BatchedTracks { tracks = toSend };
        await SendRequestAsync("/batch", batch);
    }

    public void FlushManualBatch()
    {
        Task.Run(async () => 
        {
            if (!_serverAlive) return;
            BatchedTracks batchToSend;
            
            lock (_lock)
            {
                if (_manualBatchedTracks.tracks.Count == 0) return;
                // Deep copy or reassign to avoid race conditions
                batchToSend = new BatchedTracks { tracks = new List<Tracking>(_manualBatchedTracks.tracks) };
                _manualBatchedTracks.tracks.Clear();
            }

            await SendRequestAsync("/batch", batchToSend);
        });
    }

    // Public API
    public void TrackEvent(string eventName, Dictionary<string, object> props)
    {
        if (ShouldSkip()) return;
        ProcessTrackEvent(eventName, JsonSerializer.Serialize(props));
    }

    public void TrackEvent(string eventName, string props = "")
    {
        if (ShouldSkip()) return;
        string json = string.IsNullOrEmpty(props) ? "" : JsonSerializer.Serialize(new ValueWrapper { data = props });
        ProcessTrackEvent(eventName, json);
    }

    private bool ShouldSkip() => !_serverAlive && _isServerChecked && !_autoBatching;

    private void ProcessTrackEvent(string eventName, string value)
    {
        var t = CreateTracking(eventName, value);
        lock (_lock)
        {
            if (!_isServerChecked || _autoBatching) _internalQueue.Add(t);
            else _ = SendRequestAsync("/track", t);
        }
    }

    public void BatchedTrackEvent(string eventName, Dictionary<string, object> props)
    {
        if (!_serverAlive) return;
        lock (_lock) { _manualBatchedTracks.tracks.Add(CreateTracking(eventName, JsonSerializer.Serialize(props))); }
    }

    public void BatchedTrackEvent(string eventName, string props = "")
    {
        if (!_serverAlive) return;
        lock (_lock) { _manualBatchedTracks.tracks.Add(CreateTracking(eventName, props)); }
    }

    // Shutdown / Cleanup
    public void Shutdown()
    {
        _cts?.Cancel();

        // Move queued items to manual batch for final flush
        lock (_lock)
        {
            if (_internalQueue.Count > 0)
            {
                _manualBatchedTracks.tracks.AddRange(_internalQueue);
                _internalQueue.Clear();
            }
        }

        if (_manualBatchedTracks.tracks.Count > 0 && _serverAlive)
        {
            // Blocking call to ensure data is sent before process exit
            var task = SendRequestAsync("/batch", _manualBatchedTracks);
            task.Wait(2000); 
        }
    }
}