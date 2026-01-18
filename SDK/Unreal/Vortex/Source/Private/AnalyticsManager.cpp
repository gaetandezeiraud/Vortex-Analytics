#include "AnalyticsManager.h"
#include "VortexSettings.h"
#include "Kismet/GameplayStatics.h"
#include "JsonObjectConverter.h"
#include "Misc/Guid.h"
#include "Misc/ConfigCacheIni.h"
#include "TimerManager.h"
#include "HttpModule.h"

UAnalyticsManager* UAnalyticsManager::Get(const UObject* WorldContextObject)
{
    if (UGameInstance* GI = UGameplayStatics::GetGameInstance(WorldContextObject))
        return GI->GetSubsystem<UAnalyticsManager>();
    return nullptr;
}

void UAnalyticsManager::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);
}

void UAnalyticsManager::Deinitialize()
{
    FScopeLock Lock(&QueueLock);
    if (InternalQueue.Num() > 0)
    {
        ManualBatchedTracks.tracks.Append(InternalQueue);
        InternalQueue.Empty();
    }
    
    if (ManualBatchedTracks.tracks.Num() > 0)
        PostBatchRoutine();

    Super::Deinitialize();
}

void UAnalyticsManager::Init(FString InTenantId, FString InUrl, FString InPlatform)
{
    if (bInitialized) return;
    TenantId = InTenantId;
    Url = InUrl;
    Platform = InPlatform;
    InternalInitialize();
}

void UAnalyticsManager::InternalInitialize()
{
    const UVortexSettings* Settings = GetDefault<UVortexSettings>();
    if (!Settings || !Settings->bEnabled) return;

    // Check Editor
    if (GIsEditor && !Settings->bEnableInEditor) return;

    // Check Shipping
    if (!Settings->bEnableInShipping) return;

    if (bInitialized) return;
    bInitialized = true;
    InitSession();
    CheckServerAvailability();
    TrackEvent(TEXT("app_started"));
}

void UAnalyticsManager::InitSession()
{
    FString AnonymousID;
    bool bFound = GConfig->GetString(
        TEXT("Vortex"), 
        TEXT("VortexID"), 
        AnonymousID, 
        GGameIni
    );

    if (!bFound || AnonymousID.IsEmpty())
    {
        AnonymousID = FGuid::NewGuid().ToString();
        
        GConfig->SetString(
            TEXT("Vortex"), 
            TEXT("VortexID"), 
            *AnonymousID, 
            GGameIni
        );

        GConfig->Flush(false, GGameIni);
    }

    Identity = AnonymousID;

    SessionId = FGuid::NewGuid().ToString();

    GConfig->GetString(
        TEXT("/Script/EngineSettings.GeneralProjectSettings"), 
        TEXT("ProjectVersion"), 
        AppVersion, 
        GGameIni
    );
    
    if(AppVersion.IsEmpty()) AppVersion = TEXT("1.0.0");
}

FTracking UAnalyticsManager::CreateTracking(FString Name, FString Value)
{
    FTracking NewTracking;
    NewTracking.tenant_id = TenantId;
    NewTracking.tracking.name = Name;
    NewTracking.tracking.value = Value;
    NewTracking.tracking.identity = Identity;
    NewTracking.tracking.session_id = SessionId;
    NewTracking.tracking.platform = Platform;
    NewTracking.tracking.app_version = AppVersion;
    NewTracking.tracking.timestamp = GetTimestamp();
    return NewTracking;
}

void UAnalyticsManager::CheckServerAvailability()
{
    if (Url.IsEmpty()) return;
    TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
    Request->SetVerb("GET");
    Request->SetURL(Url + "/health");
    Request->OnProcessRequestComplete().BindUObject(this, &UAnalyticsManager::OnCheckServerComplete);
    Request->ProcessRequest();
}

void UAnalyticsManager::OnCheckServerComplete(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bWasSuccessful)
{
    bServerAlive = bWasSuccessful && Response.IsValid() && EHttpResponseCodes::IsOk(Response->GetResponseCode());
    bIsServerChecked = true;

    if (bServerAlive)
    {
        if (bAutoBatching) StartAutoFlushTimer();
        else {
            FScopeLock Lock(&QueueLock);
            if (InternalQueue.Num() > 0 && GetWorld())
                GetWorld()->GetTimerManager().SetTimerForNextTick(this, &UAnalyticsManager::FlushInternalQueue);
        }
    }
}

void UAnalyticsManager::SetAutoBatching(bool bEnabled, float Interval)
{
    bAutoBatching = bEnabled;
    AutoFlushInterval = Interval;
    if (bServerAlive && bAutoBatching) StartAutoFlushTimer();
    else StopAutoFlushTimer();
}

void UAnalyticsManager::StartAutoFlushTimer()
{
    if (GetWorld()) GetWorld()->GetTimerManager().SetTimer(AutoFlushTimerHandle, this, &UAnalyticsManager::FlushInternalQueue, AutoFlushInterval, true);
}

void UAnalyticsManager::StopAutoFlushTimer()
{
    if (GetWorld()) GetWorld()->GetTimerManager().ClearTimer(AutoFlushTimerHandle);
}

void UAnalyticsManager::TrackEvent(FString EventName, FString Props)
{
    ProcessTrackEvent(EventName, Props);
}

void UAnalyticsManager::TrackEventWithProps(FString EventName, TMap<FString, FString> Props)
{
    TrackEvent(EventName, SerializeMap(Props));
}

void UAnalyticsManager::ProcessTrackEvent(FString EventName, FString Value)
{
    const UVortexSettings* Settings = GetDefault<UVortexSettings>();
    if (!Settings || !Settings->bEnabled) return;

    FTracking TrackingObj = CreateTracking(EventName, Value);
    FScopeLock Lock(&QueueLock);
    if (!bIsServerChecked || bAutoBatching) InternalQueue.Add(TrackingObj);
    else PostSingle(TrackingObj);
}

void UAnalyticsManager::BatchedTrackEvent(FString EventName, FString Props)
{
    if (!bServerAlive) return;
    FScopeLock Lock(&QueueLock);
    ManualBatchedTracks.tracks.Add(CreateTracking(EventName, Props));
}

void UAnalyticsManager::FlushManualBatch()
{
    PostBatchRoutine();
}

void UAnalyticsManager::PostSingle(const FTracking& Tracking)
{
    TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
    Request->SetVerb("POST");
    Request->SetURL(Url + "/track");
    Request->SetHeader("Content-Type", "application/json");
    Request->SetContentAsString(SerializeTracking(Tracking));
    Request->ProcessRequest();
}

void UAnalyticsManager::PostBatchRoutine()
{
    if (!bServerAlive) return;
    FString JsonPayload;
    {
        FScopeLock Lock(&QueueLock);
        if (ManualBatchedTracks.tracks.Num() == 0) return;
        JsonPayload = SerializeBatch(ManualBatchedTracks);
        ManualBatchedTracks.tracks.Empty();
    }
    TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
    Request->SetVerb("POST");
    Request->SetURL(Url + "/batch");
    Request->SetHeader("Content-Type", "application/json");
    Request->SetContentAsString(JsonPayload);
    Request->ProcessRequest();
}

void UAnalyticsManager::FlushInternalQueue()
{
    FBatchedTracks BatchToSend;
    {
        FScopeLock Lock(&QueueLock);
        if (InternalQueue.Num() == 0) return;
        BatchToSend.tracks = InternalQueue;
        InternalQueue.Empty();
    }
    TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
    Request->SetVerb("POST");
    Request->SetURL(Url + "/batch");
    Request->SetHeader("Content-Type", "application/json");
    Request->SetContentAsString(SerializeBatch(BatchToSend));
    Request->ProcessRequest();
}

FString UAnalyticsManager::SerializeTracking(const FTracking& Tracking)
{
    FString OutputString;
    FJsonObjectConverter::UStructToJsonObjectString(Tracking, OutputString);
    return OutputString;
}

FString UAnalyticsManager::SerializeBatch(const FBatchedTracks& Batch)
{
    FString OutputString;
    FJsonObjectConverter::UStructToJsonObjectString(Batch, OutputString);
    return OutputString;
}

FString UAnalyticsManager::SerializeMap(const TMap<FString, FString>& Map)
{
    TSharedPtr<FJsonObject> JsonObj = MakeShareable(new FJsonObject);
    for (const auto& Pair : Map) JsonObj->SetStringField(Pair.Key, Pair.Value);
    FString OutputString;
    TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&OutputString);
    FJsonSerializer::Serialize(JsonObj.ToSharedRef(), Writer);
    return OutputString;
}

FString UAnalyticsManager::GetTimestamp()
{
    return FDateTime::UtcNow().ToIso8601();
}