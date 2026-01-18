#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Http.h"
#include "AnalyticsManager.generated.h"

USTRUCT()
struct FTrackingData
{
    GENERATED_BODY()
    UPROPERTY() FString name;
    UPROPERTY() FString value;
    UPROPERTY() FString identity;
    UPROPERTY() FString session_id;
    UPROPERTY() FString platform;
    UPROPERTY() FString app_version;
    UPROPERTY() FString timestamp;
};

USTRUCT()
struct FTracking
{
    GENERATED_BODY()
    UPROPERTY() FString tenant_id;
    UPROPERTY() FTrackingData tracking;
};

USTRUCT()
struct FBatchedTracks
{
    GENERATED_BODY()
    UPROPERTY() TArray<FTracking> tracks;
};

UCLASS(BlueprintType)
class VORTEX_API UAnalyticsManager : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    static UAnalyticsManager* Get(const UObject* WorldContextObject);

    UFUNCTION(BlueprintCallable, Category = "Vortex")
    void Init(FString TenantId, FString Url, FString Platform);

    UFUNCTION(BlueprintCallable, Category = "Vortex")
    void TrackEvent(FString EventName, FString Props = "");

    UFUNCTION(BlueprintCallable, Category = "Vortex")
    void TrackEventWithProps(FString EventName, TMap<FString, FString> Props);

    UFUNCTION(BlueprintCallable, Category = "Vortex")
    void BatchedTrackEvent(FString EventName, FString Props = "");

    UFUNCTION(BlueprintCallable, Category = "Vortex")
    void FlushManualBatch();

    UFUNCTION(BlueprintCallable, Category = "Vortex")
    void SetAutoBatching(bool bEnabled, float Interval = 10.0f);

protected:
    virtual void Initialize(FSubsystemCollectionBase& Collection) override;
    virtual void Deinitialize() override;

private:
    FString TenantId;
    FString Url = TEXT("https://in.vortexanalytics.io");
    FString Platform;
    bool bAutoBatching = false;
    float AutoFlushInterval = 10.0f;
    FTimerHandle AutoFlushTimerHandle;

    FString Identity;
    FString SessionId;
    FString AppVersion;

    bool bIsServerChecked = false;
    bool bServerAlive = false;
    bool bInitialized = false;

    TArray<FTracking> InternalQueue;
    FBatchedTracks ManualBatchedTracks;
    FCriticalSection QueueLock;

    void InternalInitialize();
    void InitSession();
    void CheckServerAvailability();
    void OnCheckServerComplete(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bWasSuccessful);
    void ProcessTrackEvent(FString EventName, FString Value);
    void StartAutoFlushTimer();
    void StopAutoFlushTimer();
    void FlushInternalQueue();
    void PostSingle(const FTracking& Tracking);
    void PostBatchRoutine();
    
    FTracking CreateTracking(FString Name, FString Value);
    FString SerializeTracking(const FTracking& Tracking);
    FString SerializeBatch(const FBatchedTracks& Batch);
    FString SerializeMap(const TMap<FString, FString>& Map);
    FString GetTimestamp();
};