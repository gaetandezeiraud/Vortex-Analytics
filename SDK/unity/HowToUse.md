# AnalyticsManager Documentation

`AnalyticsManager` is a Unity component responsible for sending analytics events to a remote server.  
It supports:

- Immediate event tracking  
- Batched event tracking  
- Automatic retry when the server becomes available  
- Session and device identification  
- Manual or automatic initialization

## Initialization

### Automatic Initialization (Recommended)

Attach the `AnalyticsManager` component to a GameObject and configure:

- **Tenant ID**
- **Server URL**
- **Platform**

When `Initialize On Start` is enabled, the system initializes automatically during `Awake()`.

### Manual Initialization

If you need to initialize analytics at runtime (e.g., after login):

```csharp
AnalyticsManager.Instance.Init(
    tenantId: "mygame",
    url: "https://analytics.myserver.com",
    platform: "STEAM"
);
```

⚠️ This must be called before sending any events.

### Internal Behavior

On initialization, the system:
1. Generates or loads a persistent device identifier
2. Creates a new session ID
3. Performs a server health check
4. Enables or disables analytics based on server availability

If the server is unreachable, events are safely queued until connectivity is restored.

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

### Manual Batching
Manual batching allows you to explicitly control when analytics events are sent.

#### Add Events to Batch

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

#### Send Batched Events

```csharp
AnalyticsManager.Instance.FlushManualBatch();
```

All queued events will be sent in a single request.

## Automatic Batching

When Auto Batching is enabled:

- Events are queued automatically
- The system sends batches every Auto Flush Interval seconds

If the server is unreachable, events remain queued.

##  Lifecycle Handling

Analytics are flushed automatically when:
- The application loses focus (mobile background)
- The application is quitting

This ensures minimal data loss.