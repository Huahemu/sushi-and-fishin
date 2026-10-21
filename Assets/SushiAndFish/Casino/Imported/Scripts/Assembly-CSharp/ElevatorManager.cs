namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::SkyBrave_Toolkit.Scripts.Scriptable_Game_Events;
using global::UnityEngine;

public class ElevatorManager : NetworkSingleton<ElevatorManager>
{
	[Header("References")]
	[SerializeField]
	private Transform rightDoor;

	[SerializeField]
	private Transform leftDoor;

	public Transform playerSpawnPosition;

	[SerializeField]
	private List<Transform> buttonList;

	[SerializeField]
	private List<CasinoFloor> allFloors;

	[SerializeField]
	private Collider checkCollider;

	[SerializeField]
	private GameEvent serverOnElevatorMoveEvent;

	[SerializeField]
	private CasinoBuilding casinoBuilding;

	[Header("Settings")]
	public float doorMoveDuration;

	public bool spawnNpc = true;

	[Header("SFX")]
	[SerializeField]
	private Transform dingSfxPos;

	[SerializeField]
	private EventReference uiSfxElevatorRise;

	[SerializeField]
	private EventReference uiSfxElevatorOpen;

	[SerializeField]
	private EventReference elevatorDingSfx;

	public bool isTeleporting;

	private Coroutine _elevatorRoutine;

	private int _currentFloorIndex;

	private bool _isLocked;

	public bool IsLocked
	{
		get
		{
			return _isLocked;
		}
		set
		{
			if (_isLocked != value)
			{
				_isLocked = value;
			}
		}
	}

	private void Start()
	{
		SetButtons();
	}

	public void Initialize()
	{
		RpcSetActiveFloorOnly(0);
	}

	private void SetButtons()
	{
		if (CasinoLab.CasinoLabMode.Active && !NetworkSingleton<GameManager>.TryGetInstance(out _)) return;
		int a = NetworkSingleton<GameManager>.Instance.currentFloor + 1;
		a = Mathf.Max(a, 1);
		for (int i = 0; i < buttonList.Count; i++)
		{
			buttonList[i].gameObject.SetActive(i <= a);
		}
	}

	[ClientRpc]
	public void RpcEnableAllButtons()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void ElevatorManager::RpcEnableAllButtons()", -1353552624, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerTryTeleportPlayers(int toIndex)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ElevatorManager::ServerTryTeleportPlayers(System.Int32)' called when server was not active");
		}
		else if (!isTeleporting && !IsLocked && _currentFloorIndex != toIndex && CheckPlayersInside())
		{
			isTeleporting = true;
			_currentFloorIndex = toIndex;
			if (_elevatorRoutine != null)
			{
				StopCoroutine(_elevatorRoutine);
			}
			_elevatorRoutine = StartCoroutine(ServerElevatorRoutine(toIndex));
		}
	}

	[Server]
	public void ServerForceTeleportPlayers(int toIndex)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ElevatorManager::ServerForceTeleportPlayers(System.Int32)' called when server was not active");
		}
		else
		{
			if (_currentFloorIndex == toIndex)
			{
				return;
			}
			isTeleporting = true;
			_currentFloorIndex = toIndex;
			foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
			{
				if (!player.carry.GetIsBeingHeld())
				{
					Vector2 vector = Random.insideUnitCircle * 3f;
					Vector3 vector2 = new Vector3(vector.x, 0f, vector.y);
					player.controller.ServerTeleport(NetworkSingleton<ElevatorManager>.Instance.playerSpawnPosition.position + vector2);
					player.controller.ServerRotate(new Vector2(180f, 0f));
				}
			}
			if (_elevatorRoutine != null)
			{
				StopCoroutine(_elevatorRoutine);
			}
			_elevatorRoutine = StartCoroutine(ServerElevatorRoutine(toIndex));
		}
	}

	private bool CheckPlayersInside()
	{
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			if (!checkCollider.bounds.Contains(player.transform.position))
			{
				return false;
			}
		}
		return true;
	}

	private IEnumerator ServerElevatorRoutine(int toIndex)
	{
		RpcMoveDoors(isOpening: false);
		yield return new WaitForSeconds(doorMoveDuration);
		foreach (ConsumableItem spawnedItemInstance in NetworkSingleton<ItemManager>.Instance.spawnedItemInstances)
		{
			if (!checkCollider.bounds.Contains(spawnedItemInstance.transform.position))
			{
				spawnedItemInstance.ServerSetEnabled(isEnabled: false);
			}
		}
		ServerTeleportPlayersOutside();
		RpcSetActiveFloorOnly(toIndex);
		RpcSetMusicPlaylistInAdvance(toIndex);
		yield return null;
		if (spawnNpc)
		{
			NetworkSingleton<NavMeshManager>.Instance.InitializeNavMesh();
			yield return null;
			yield return NetworkSingleton<NPCSpawner>.Instance.StartCoroutine(NetworkSingleton<NPCSpawner>.Instance.SpawnNPCsForFloor(toIndex));
		}
		serverOnElevatorMoveEvent?.Raise();
		if (toIndex == 0 && casinoBuilding != null)
		{
			casinoBuilding.ServerSpawnGoToHomeVehicle();
		}
		yield return new WaitForSeconds(1f);
		RpcMoveDoors(isOpening: true);
		yield return new WaitForSeconds(doorMoveDuration);
		NetworkSingleton<GameManager>.Instance.StartDay();
		isTeleporting = false;
	}

	private void ServerTeleportPlayersOutside()
	{
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			if (!player.carry.GetIsBeingHeld() && !checkCollider.bounds.Contains(player.transform.position))
			{
				Vector2 vector = Random.insideUnitCircle * 3f;
				Vector3 vector2 = new Vector3(vector.x, 0f, vector.y);
				player.controller.ServerTeleport(NetworkSingleton<ElevatorManager>.Instance.playerSpawnPosition.position + vector2);
				player.controller.ServerRotate(new Vector2(180f, 0f));
			}
		}
	}

	[ClientRpc]
	private void RpcSetActiveFloorOnly(int index)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(index);
		SendRPCInternal("System.Void ElevatorManager::RpcSetActiveFloorOnly(System.Int32)", 915214480, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcMoveDoors(bool isOpening)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isOpening);
		SendRPCInternal("System.Void ElevatorManager::RpcMoveDoors(System.Boolean)", -1745372052, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public bool IsInElevator(Vector3 position)
	{
		return checkCollider.bounds.Contains(position);
	}

	[ClientRpc]
	private void RpcSetMusicPlaylistInAdvance(int index)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(index);
		SendRPCInternal("System.Void ElevatorManager::RpcSetMusicPlaylistInAdvance(System.Int32)", -2012512390, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcEnableAllButtons()
	{
		foreach (Transform button in buttonList)
		{
			button.gameObject.SetActive(value: true);
		}
	}

	protected static void InvokeUserCode_RpcEnableAllButtons(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcEnableAllButtons called on server.");
		}
		else
		{
			((ElevatorManager)obj).UserCode_RpcEnableAllButtons();
		}
	}

	protected void UserCode_RpcSetActiveFloorOnly__Int32(int index)
	{
        CasinoRuntimeContext.FloorChanging?.Invoke(index);
		foreach (CasinoFloor allFloor in allFloors)
		{
			bool flag = allFloor.floorIndex == index;
			allFloor.gameObject.SetActive(flag);
			allFloor.SetSfxTrigger(flag);
		}
		MonoSingleton<LightbakerManager>.Instance.SetLightVolume(index);
	}

	protected static void InvokeUserCode_RpcSetActiveFloorOnly__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetActiveFloorOnly called on server.");
		}
		else
		{
			((ElevatorManager)obj).UserCode_RpcSetActiveFloorOnly__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcMoveDoors__Boolean(bool isOpening)
	{
		if (isOpening)
		{
			rightDoor.DOLocalMoveX(2.5f, doorMoveDuration).SetEase(Ease.InOutExpo);
			leftDoor.DOLocalMoveX(-2.5f, doorMoveDuration).SetEase(Ease.InOutExpo);
			SFXManager.SFXOneShot(uiSfxElevatorOpen, dingSfxPos.position);
			SFXManager.SFXOneShot(elevatorDingSfx, dingSfxPos.position);
		}
		else
		{
			rightDoor.DOLocalMoveX(0f, doorMoveDuration).SetEase(Ease.InOutExpo);
			leftDoor.DOLocalMoveX(0f, doorMoveDuration).SetEase(Ease.InOutExpo);
			SFXManager.SFXOneShot(uiSfxElevatorRise, dingSfxPos.position);
		}
	}

	protected static void InvokeUserCode_RpcMoveDoors__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcMoveDoors called on server.");
		}
		else
		{
			((ElevatorManager)obj).UserCode_RpcMoveDoors__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcSetMusicPlaylistInAdvance__Int32(int index)
	{
		string label = index switch
		{
			0 => "Lobby", 
			1 => "CasinoLevel1", 
			2 => "CasinoLevel2", 
			3 => "CasinoLevel3", 
			4 => "CasinoLevel4", 
			5 => "BossRoom", 
			_ => "", 
		};
		if (NetworkSingleton<GameManager>.Instance.state == GameState.Win && index == 1)
		{
			label = "BossRoom";
		}
		RuntimeManager.StudioSystem.setParameterByNameWithLabel("MusicPlaylist", label);
	}

	protected static void InvokeUserCode_RpcSetMusicPlaylistInAdvance__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetMusicPlaylistInAdvance called on server.");
		}
		else
		{
			((ElevatorManager)obj).UserCode_RpcSetMusicPlaylistInAdvance__Int32(reader.ReadVarInt());
		}
	}

	static ElevatorManager()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(ElevatorManager), "System.Void ElevatorManager::RpcEnableAllButtons()", InvokeUserCode_RpcEnableAllButtons);
		RemoteProcedureCalls.RegisterRpc(typeof(ElevatorManager), "System.Void ElevatorManager::RpcSetActiveFloorOnly(System.Int32)", InvokeUserCode_RpcSetActiveFloorOnly__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(ElevatorManager), "System.Void ElevatorManager::RpcMoveDoors(System.Boolean)", InvokeUserCode_RpcMoveDoors__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(ElevatorManager), "System.Void ElevatorManager::RpcSetMusicPlaylistInAdvance(System.Int32)", InvokeUserCode_RpcSetMusicPlaylistInAdvance__Int32);
	}
}

}
