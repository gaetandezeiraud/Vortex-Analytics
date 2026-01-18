using UnrealBuildTool;

public class Vortex : ModuleRules
{
	public Vortex(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] { 
			"Core", "CoreUObject", "Engine", "HTTP", "Json", "JsonUtilities", "DeveloperSettings" 
		});
	}
}