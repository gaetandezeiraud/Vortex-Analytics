# AnalyticsManager Documentation

`AnalyticsManager` is a pure C# singleton class responsible for sending analytics events to a remote server. It is designed for use in any .NET environment (WPF, Monogame, Godot, Console, ASP.NET, etc.).

It supports:

- Immediate event tracking
- Batched event tracking
- Automatic retry when the server becomes available
- Local file-based session and device identification
- Thread-safe queue management

## Dependencies

- **.NET Standard 2.0+** or **.NET 6+**
- `System.Text.Json` (Required for JSON serialization)

## Initialization

It is a Singleton that must be initialized explicitly in your application's entry point (e.g., `Main()`, `App.xaml.cs`, `Game1.cs`).

### Basic Setup

```csharp
// 1. Configure settings (Optional)
// Set to true to queue events and send them periodically instead of immediately.
// Default is false (immediate send).
bool enableAutoBatching = true; 
int flushIntervalSeconds = 10;

// 2. Initialize the Singleton
AnalyticsManager.Instance.Init(
    tenantId: "mygame_production",
    url: "https://analytics.myserver.com",
    platform: "Windows",
    appVersion: "1.0.0",
    autoBatching: enableAutoBatching,
    flushIntervalSec: flushIntervalSeconds
);
```

> ⚠️ Important: Init() must be called before sending any events.

### Internal Behavior

On initialization, the system:

1. Loads or generates a persistent device identifier (stored in a local file).
2. Creates a new session ID.
3. Starts a background task to check server health.
4. Enables or disables analytics based on server availability.

If the server is unreachable, events are safely queued in memory until connectivity is restored.

## Tracking Events

### Simple Event
```csharp
AnalyticsManager.Instance.TrackEvent("app_started");
```

### Event with String Payload
```csharp
AnalyticsManager.Instance.TrackEvent("menu_opened", "settings");
```

### Event with Structured Data
```csharp
AnalyticsManager.Instance.TrackEvent("level_completed", new Dictionary<string, object>
{
    { "level", 5 },
    { "difficulty", "Hard" },
    { "time", 123.4f }
});
```

## Batching

### Manual Batching
Manual batching allows you to explicitly control when analytics events are sent (e.g., at the end of a match).

### Add Events to Batch

```csharp
AnalyticsManager.Instance.BatchedTrackEvent("EnemyKilled");

AnalyticsManager.Instance.BatchedTrackEvent(
    "ItemCrafted",
    new Dictionary<string, object>
    {
        { "item", "MagicSword" },
        { "rarity", "Epic" }
    }
);
```

### Send Batched Events

```csharp
AnalyticsManager.Instance.FlushManualBatch();
```

This triggers a background task to send all queued events in a single request.

### Automatic Batching

When `autoBatching` is enabled during initialization:

Events tracked via TrackEvent are queued automatically.

The system flushes the queue every flushIntervalSec seconds.

If the server is unreachable, events remain queued until the server responds to a health check.

## Lifecycle Handling

Because this is a standard C# class, it cannot automatically detect when your application closes. You must call `Shutdown()` when your application exits to ensure buffered events are flushed to the network.

### Monogame Example (`Game1.cs`)

```csharp
protected override void UnloadContent()
{
    AnalyticsManager.Instance.Shutdown();
    base.UnloadContent();
}
```

### WPF Example (`App.xaml.cs`)

```csharp
protected override void OnExit(ExitEventArgs e)
{
    AnalyticsManager.Instance.Shutdown();
    base.OnExit(e);
}
```

### Console App Example

```csharp
AppDomain.CurrentDomain.ProcessExit += (s, e) => 
{
    AnalyticsManager.Instance.Shutdown();
};
```

Calling `Shutdown()` performs a blocking wait (max 2 seconds) to attempt a final data flush before the process terminates.