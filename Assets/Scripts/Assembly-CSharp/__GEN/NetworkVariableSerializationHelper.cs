using PaperPixelGames.Core.Blackjack;
using PaperPixelGames.Core.Boss;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Core.Surf;
using PaperPixelGames.Framework.Enums;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace __GEN
{
	internal class NetworkVariableSerializationHelper
	{
		[RuntimeInitializeOnLoadMethod]
		internal static void InitializeSerialization()
		{
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<bool>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<bool>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<float>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<float>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<CustomSushiRecipeNetworkData>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<CustomSushiRecipeNetworkData>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<byte>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<byte>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<ToolDataNet>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<ToolDataNet>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<OctopusTrapState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<OctopusTrapState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<NetworkObjectReference>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<NetworkObjectReference>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<Vector3>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<Vector3>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_FixedString<FixedString64Bytes>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<FixedString64Bytes>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<FishBookEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<FishBookEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<FishPersonalBestEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<FishPersonalBestEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<CustomerOrderEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<CustomerOrderEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<DailySushiSpecialEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<DailySushiSpecialEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<SurfboardState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<SurfboardState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<double>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<double>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<SurfWave>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<SurfWave>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<StaffEmployeeEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<StaffEmployeeEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_FixedString<FixedString512Bytes>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<FixedString512Bytes>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<StaffRole>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<StaffRole>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<Vector2>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<Vector2>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<ProgressionNetData>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<ProgressionNetData>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<PoseidonTridentState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<PoseidonTridentState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<BeverageShelfState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<BeverageShelfState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<FilletState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<FilletState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<IngredientEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<IngredientEntry>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<sbyte>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<sbyte>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_FixedString<FixedString128Bytes>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<FixedString128Bytes>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<SushiCompositionNetworkData>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<SushiCompositionNetworkData>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<ChefDeskSushiRoll.RollNetworkState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<ChefDeskSushiRoll.RollNetworkState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<BeachBall.MotionState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<BeachBall.MotionState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<ConveyorBeltTableState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<ConveyorBeltTableState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<ConveyorBeltTable.DirtyPlateSnapshot>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<ConveyorBeltTable.DirtyPlateSnapshot>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<RestaurantTableState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<RestaurantTableState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<Color>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<Color>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<CrabBossEncounterState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<CrabBossEncounterState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<CrabBossAttackId>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<CrabBossAttackId>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedByMemcpy<BlackjackTablePhase>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedValueEquals<BlackjackTablePhase>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<BlackjackPlayerState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<BlackjackPlayerState>();
			NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<BlackjackCardState>();
			NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<BlackjackCardState>();
		}
	}
}
