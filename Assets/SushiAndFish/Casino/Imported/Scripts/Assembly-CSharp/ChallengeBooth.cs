namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.Events;

public class ChallengeBooth : NetworkBehaviour
{
	private ChallengeSettings _challengeSettings;

	private GameSettings _gameSettings;

	[Header("Settings")]
	[SerializeField]
	private int dailyChallengeLimit;

	[SyncVar]
	private int lastDayChallengeGiven = -1;

	[Header("Trigger Events")]
	[SerializeField]
	private UnityEvent onAllPlayersInside;

	private List<PlayerController> playersInside = new List<PlayerController>();

	[SerializeField]
	private bool debugMode;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent rerollChallengeSfx;

	[SerializeField]
	private SFXComponent invalidInteractionSfx;

	public int NetworklastDayChallengeGiven
	{
		get
		{
			return lastDayChallengeGiven;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref lastDayChallengeGiven, 1uL, null);
		}
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		_challengeSettings = SushiAndFish.Casino.Original.CasinoResources.Load<ChallengeSettings>("ChallengeSettings");
		_gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		dailyChallengeLimit = _challengeSettings.dailyAvailableChallengeCount;
	}

	[Server]
	public void TryGiveDailyChallenge()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeBooth::TryGiveDailyChallenge()' called when server was not active");
		}
		else
		{
			if (NetworkSingleton<GameManager>.Instance == null)
			{
				return;
			}
			int daysPassed = NetworkSingleton<GameManager>.Instance.daysPassed;
			if (lastDayChallengeGiven == daysPassed)
			{
				return;
			}
			List<ChallengeProgress> activeChallenges = NetworkSingleton<ChallengeManager>.Instance.GetActiveChallenges();
			if (activeChallenges != null && activeChallenges.Count > 0)
			{
				foreach (ChallengeProgress item in activeChallenges)
				{
					NetworkSingleton<ChallengeManager>.Instance.DeactivateChallenge(item.challenge);
				}
			}
			GiveChallengeInternal();
			NetworklastDayChallengeGiven = daysPassed;
		}
	}

	[Server]
	public void RerollChallenge()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeBooth::RerollChallenge()' called when server was not active");
			return;
		}
		int challengeRerollPrice = _challengeSettings.challengeRerollPrice;
		if (NetworkSingleton<MoneyManager>.Instance == null)
		{
			Debug.LogWarning("[ChallengeBooth] MoneyManager not found - cannot reroll challenge");
			return;
		}
		if (NetworkSingleton<MoneyManager>.Instance.ticketBalance < challengeRerollPrice)
		{
			Debug.Log($"[ChallengeBooth] Not enough tickets to reroll! Need {challengeRerollPrice}, have {NetworkSingleton<MoneyManager>.Instance.ticketBalance}");
			RpcNotifyRerollFailed("Not enough tickets");
			return;
		}
		List<ChallengeProgress> activeChallenges = NetworkSingleton<ChallengeManager>.Instance.GetActiveChallenges();
		HashSet<Challenge> hashSet = new HashSet<Challenge>(from p in activeChallenges
			select p.challenge into c
			where c != null
			select c);
		int currentFloor = NetworkSingleton<GameManager>.Instance.currentFloor;
		List<Challenge> challengesByFloorIndex = NetworkSingleton<ChallengeManager>.Instance.GetChallengesByFloorIndex(currentFloor);
		if (challengesByFloorIndex == null)
		{
			return;
		}
		int num = currentFloor + 1;
		HashSet<CasinoGameType> availableGameTypesForFloor = NextCasinoPredicter.GetAvailableGameTypesForFloor(num);
		if (debugMode)
		{
			Debug.Log(string.Format("[ChallengeBooth.RerollChallenge] Floor {0} (loot table {1}) - Available game types: {2}", currentFloor, num, string.Join(", ", availableGameTypesForFloor)));
			Debug.Log($"[ChallengeBooth.RerollChallenge] Total challenges for floor {currentFloor}: {challengesByFloorIndex.Count}");
		}
		List<(Challenge, HashSet<CasinoGameType>)> list = new List<(Challenge, HashSet<CasinoGameType>)>();
		List<Challenge> list2 = new List<Challenge>();
		foreach (Challenge item2 in challengesByFloorIndex)
		{
			if (item2 == null)
			{
				continue;
			}
			HashSet<CasinoGameType> requiredGameTypes = item2.GetRequiredGameTypes();
			if (requiredGameTypes == null || requiredGameTypes.Count == 0)
			{
				list2.Add(item2);
				if (debugMode)
				{
					Debug.Log("[ChallengeBooth.RerollChallenge] ✓ '" + item2.challengeName + "' - Game-agnostic (allowed)");
				}
			}
			else if (requiredGameTypes.Overlaps(availableGameTypesForFloor))
			{
				list2.Add(item2);
				if (debugMode)
				{
					Debug.Log("[ChallengeBooth.RerollChallenge] ✓ '" + item2.challengeName + "' - Requires " + string.Join(", ", requiredGameTypes) + " (available)");
				}
			}
			else
			{
				list.Add((item2, requiredGameTypes));
				if (debugMode)
				{
					Debug.Log("[ChallengeBooth.RerollChallenge] ✗ '" + item2.challengeName + "' - Requires " + string.Join(", ", requiredGameTypes) + " (NOT available - FILTERED OUT)");
				}
			}
		}
		if (debugMode)
		{
			Debug.Log($"[ChallengeBooth.RerollChallenge] Valid challenges: {list2.Count}, Filtered out: {list.Count}");
		}
		if (list2.Count == 0)
		{
			Debug.LogWarning($"[ChallengeBooth] No valid challenges available for floor {currentFloor} (all require unavailable games).");
			return;
		}
		int successfulQuota = NetworkSingleton<GameManager>.Instance.successfulQuota;
		int daysPassed = NetworkSingleton<GameManager>.Instance.daysPassed;
		global::System.Random random = new global::System.Random(GetDeterministicChallengeHash(NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed, successfulQuota, daysPassed));
		List<Challenge> list3 = list2.ToList();
		for (int num2 = list3.Count - 1; num2 > 0; num2--)
		{
			int index = random.Next(0, num2 + 1);
			Challenge value = list3[num2];
			list3[num2] = list3[index];
			list3[index] = value;
		}
		int rerollAttempt = 0;
		if (hashSet.Count > 0)
		{
			Challenge item = hashSet.First();
			int num3 = list3.IndexOf(item);
			rerollAttempt = ((num3 < 0) ? hashSet.Count : (num3 + 1));
		}
		HashSet<Challenge> excludeChallenges = new HashSet<Challenge>(hashSet);
		if (activeChallenges != null && activeChallenges.Count > 0)
		{
			foreach (ChallengeProgress item3 in activeChallenges)
			{
				if (item3.challenge != null)
				{
					NetworkSingleton<ChallengeManager>.Instance.DeactivateChallenge(item3.challenge);
				}
			}
		}
		NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(-challengeRerollPrice);
		GiveChallengeInternal(excludeChallenges, rerollAttempt);
		RpcNotifyChallengeRerolled(challengeRerollPrice);
	}

	[Server]
	private void GiveChallengeInternal(HashSet<Challenge> excludeChallenges = null, int rerollAttempt = -1)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeBooth::GiveChallengeInternal(System.Collections.Generic.HashSet`1<Challenge>,System.Int32)' called when server was not active");
			return;
		}
		int currentFloor = NetworkSingleton<GameManager>.Instance.currentFloor;
		List<Challenge> challengesByFloorIndex = NetworkSingleton<ChallengeManager>.Instance.GetChallengesByFloorIndex(currentFloor);
		if (challengesByFloorIndex == null || challengesByFloorIndex.Count == 0)
		{
			Debug.LogWarning($"[ChallengeBooth] No challenges available for floor index: {currentFloor}");
			return;
		}
		HashSet<Challenge> collection = new HashSet<Challenge>(from p in NetworkSingleton<ChallengeManager>.Instance.GetActiveChallenges()
			select p.challenge into c
			where c != null
			select c);
		HashSet<Challenge> excludedSet = new HashSet<Challenge>(collection);
		if (excludeChallenges != null)
		{
			excludedSet.UnionWith(excludeChallenges);
		}
		int num = currentFloor + 1;
		HashSet<CasinoGameType> availableGameTypesForFloor = NextCasinoPredicter.GetAvailableGameTypesForFloor(num);
		if (debugMode)
		{
			Debug.Log(string.Format("[ChallengeBooth] Floor {0} (loot table {1}) - Available game types: {2}", currentFloor, num, string.Join(", ", availableGameTypesForFloor)));
			Debug.Log($"[ChallengeBooth] Total challenges for floor {currentFloor}: {challengesByFloorIndex.Count}");
		}
		List<(Challenge, HashSet<CasinoGameType>)> list = new List<(Challenge, HashSet<CasinoGameType>)>();
		List<Challenge> list2 = new List<Challenge>();
		foreach (Challenge item in challengesByFloorIndex)
		{
			if (item == null)
			{
				continue;
			}
			HashSet<CasinoGameType> requiredGameTypes = item.GetRequiredGameTypes();
			if (requiredGameTypes == null || requiredGameTypes.Count == 0)
			{
				list2.Add(item);
				if (debugMode)
				{
					Debug.Log("[ChallengeBooth] ✓ '" + item.challengeName + "' - Game-agnostic (allowed)");
				}
			}
			else if (requiredGameTypes.Overlaps(availableGameTypesForFloor))
			{
				list2.Add(item);
				if (debugMode)
				{
					Debug.Log("[ChallengeBooth] ✓ '" + item.challengeName + "' - Requires " + string.Join(", ", requiredGameTypes) + " (available)");
				}
			}
			else
			{
				list.Add((item, requiredGameTypes));
				if (debugMode)
				{
					Debug.Log("[ChallengeBooth] ✗ '" + item.challengeName + "' - Requires " + string.Join(", ", requiredGameTypes) + " (NOT available - FILTERED OUT)");
				}
			}
		}
		if (debugMode)
		{
			Debug.Log($"[ChallengeBooth] Valid challenges: {list2.Count}, Filtered out: {list.Count}");
		}
		if (list2.Count == 0)
		{
			Debug.LogWarning($"[ChallengeBooth] No valid challenges available for floor {currentFloor} (all require unavailable games).");
			return;
		}
		int successfulQuota = NetworkSingleton<GameManager>.Instance.successfulQuota;
		int daysPassed = NetworkSingleton<GameManager>.Instance.daysPassed;
		if (rerollAttempt < 0)
		{
			rerollAttempt = excludedSet.Count;
		}
		global::System.Random random = new global::System.Random(GetDeterministicChallengeHash(NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed, successfulQuota, daysPassed));
		List<Challenge> list3 = list2.ToList();
		for (int num2 = list3.Count - 1; num2 > 0; num2--)
		{
			int index = random.Next(0, num2 + 1);
			Challenge value = list3[num2];
			list3[num2] = list3[index];
			list3[index] = value;
		}
		Challenge challenge = null;
		int num3 = 0;
		while (challenge == null && num3 < list3.Count)
		{
			int index2 = (rerollAttempt + num3) % list3.Count;
			Challenge challenge2 = list3[index2];
			if (!excludedSet.Contains(challenge2))
			{
				challenge = challenge2;
				break;
			}
			num3++;
		}
		if (challenge == null)
		{
			List<Challenge> list4 = list2.Where((Challenge c) => !excludedSet.Contains(c)).ToList();
			if (list4.Count <= 0)
			{
				Debug.LogWarning($"[ChallengeBooth] Could not find any available challenge for floor {currentFloor} after filtering.");
				return;
			}
			challenge = list4[0];
		}
		Challenge challenge3 = challenge;
		Debug.Log($"[ChallengeBooth] Selected challenge: '{challenge3.challengeName}' (rerollAttempt: {rerollAttempt}, index in shuffled: {list3.IndexOf(challenge3)})");
		NetworkSingleton<ChallengeManager>.Instance.ActivateChallenge(challenge3);
		RpcNotifyChallengePurchased(challenge3.challengeName, currentFloor);
	}

	private int GetDeterministicChallengeHash(int seed, int quotaIndex, int day, int rerollAttempt = 0)
	{
		return ((seed * 31 + quotaIndex) * 31 + day) * 31 + rerollAttempt;
	}

	[ClientRpc]
	private void RpcNotifyChallengePurchased(string challengeName, int floorIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(challengeName);
		writer.WriteVarInt(floorIndex);
		SendRPCInternal("System.Void ChallengeBooth::RpcNotifyChallengePurchased(System.String,System.Int32)", -963902717, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcNotifyChallengeRerolled(int cost)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(cost);
		SendRPCInternal("System.Void ChallengeBooth::RpcNotifyChallengeRerolled(System.Int32)", 345287561, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcNotifyRerollFailed(string reason)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(reason);
		SendRPCInternal("System.Void ChallengeBooth::RpcNotifyRerollFailed(System.String)", 1133253287, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (base.isServer && !(other.attachedRigidbody == null) && other.attachedRigidbody.TryGetComponent<PlayerController>(out var component) && !playersInside.Contains(component))
		{
			playersInside.Add(component);
			CheckAllPlayersInside();
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (base.isServer && !(other.attachedRigidbody == null) && other.attachedRigidbody.TryGetComponent<PlayerController>(out var component))
		{
			playersInside.Remove(component);
		}
	}

	[Server]
	private void CheckAllPlayersInside()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeBooth::CheckAllPlayersInside()' called when server was not active");
			return;
		}
		List<PlayerController> list = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).ToList();
		if (list.Count == 0 || playersInside.Count < list.Count)
		{
			return;
		}
		bool flag = true;
		foreach (PlayerController item in list)
		{
			if (!playersInside.Contains(item))
			{
				flag = false;
				break;
			}
		}
		if (flag)
		{
			onAllPlayersInside?.Invoke();
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcNotifyChallengePurchased__String__Int32(string challengeName, int floorIndex)
	{
		Debug.Log($"[ChallengeBooth] Received challenge: {challengeName} ({floorIndex})");
	}

	protected static void InvokeUserCode_RpcNotifyChallengePurchased__String__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcNotifyChallengePurchased called on server.");
		}
		else
		{
			((ChallengeBooth)obj).UserCode_RpcNotifyChallengePurchased__String__Int32(reader.ReadString(), reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcNotifyChallengeRerolled__Int32(int cost)
	{
		Debug.Log($"[ChallengeBooth] Challenge rerolled for {cost} ticket(s)");
		if (rerollChallengeSfx != null)
		{
			rerollChallengeSfx.PlayOneShotWith3DPos();
		}
	}

	protected static void InvokeUserCode_RpcNotifyChallengeRerolled__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcNotifyChallengeRerolled called on server.");
		}
		else
		{
			((ChallengeBooth)obj).UserCode_RpcNotifyChallengeRerolled__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcNotifyRerollFailed__String(string reason)
	{
		Debug.Log("[ChallengeBooth] Failed to reroll challenge: " + reason);
		if (invalidInteractionSfx != null)
		{
			invalidInteractionSfx.PlayOneShotWith3DPos();
		}
	}

	protected static void InvokeUserCode_RpcNotifyRerollFailed__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcNotifyRerollFailed called on server.");
		}
		else
		{
			((ChallengeBooth)obj).UserCode_RpcNotifyRerollFailed__String(reader.ReadString());
		}
	}

	static ChallengeBooth()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(ChallengeBooth), "System.Void ChallengeBooth::RpcNotifyChallengePurchased(System.String,System.Int32)", InvokeUserCode_RpcNotifyChallengePurchased__String__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(ChallengeBooth), "System.Void ChallengeBooth::RpcNotifyChallengeRerolled(System.Int32)", InvokeUserCode_RpcNotifyChallengeRerolled__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(ChallengeBooth), "System.Void ChallengeBooth::RpcNotifyRerollFailed(System.String)", InvokeUserCode_RpcNotifyRerollFailed__String);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(lastDayChallengeGiven);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(lastDayChallengeGiven);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref lastDayChallengeGiven, null, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref lastDayChallengeGiven, null, reader.ReadVarInt());
		}
	}
}

}
