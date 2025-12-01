
AnalyticsManager is a Unity component responsible for sending analytics events to a remote server.   
It supports:
- Single events
- Events
- Batched events
- Automatic device identity, session IDs, version tracking

> At startup, the system performs a health check to verify whether the analytics API is reachable.
If the API is not available, analytics will be disabled for the entire session to avoid performance degradation caused by repeated timeouts.

# Setup

You have two ways to configure the analytics system:

## Setup via Unity Inspector (Recommended)

Attach the AnalyticsManager component to a GameObject, then set:

```
Tenant ID
Server URL
Platform
```

Example:

```
Tenant ID: mygame
URL: https://analytics.myserver.com
Platform: STEAM
```

## Setup via Script using Init()

If you want to configure it dynamically: 

```csharp
AnalyticsManager.Instance.Init(
    tenantId: "mygame",
    url: "https://analytics.myserver.com",
    platform: "STEAM"
);
```

Call this before tracking any events.

# Tracking Events
## Simple event (no properties)

```csharp
AnalyticsManager.Instance.TrackEvent("app_started");
```

## Event with string value

```csharp
AnalyticsManager.Instance.TrackEvent("page", "themes");
```

## Event with dictionary

```csharp
AnalyticsManager.Instance.TrackEvent("LevelCompleted", {
    { "level", 5 },
    { "difficulty", "Hard" },
    { "time", 123.4f }
});
```

# Batched Events

Batched events are stored and sent later using PostBatchAsync().

## Event to batch

```csharp
AnalyticsManager.Instance.BatchedTrackEvent("EnemyKilled");
```

Or 

```csharp
AnalyticsManager.Instance.BatchedTrackEvent("ItemCrafted", {
    { "item", "MagicSword" },
    { "rarity", "Epic" }
});
```

##  Send all batched events

```csharp
await AnalyticsManager.Instance.PostBatchAsync();
```

After sending, the batch is automatically cleared.