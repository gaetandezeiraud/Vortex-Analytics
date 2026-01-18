#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "VortexSettings.generated.h"

UCLASS(Config=Game, DefaultConfig, meta=(DisplayName="Vortex Analytics"))
class VORTEX_API UVortexSettings : public UDeveloperSettings
{
    GENERATED_BODY()

public:
    UPROPERTY(Config, EditAnywhere, Category="General")
    bool bEnabled = true;

    UPROPERTY(Config, EditAnywhere, Category="General")
    bool bEnableInEditor = true;

    UPROPERTY(Config, EditAnywhere, Category="General")
    bool bEnableInShipping = true;

    virtual FName GetContainerName() const override { return TEXT("Project"); }
    virtual FName GetCategoryName() const override { return TEXT("Plugins"); }
    virtual FName GetSectionName() const override { return TEXT("Vortex"); }
};