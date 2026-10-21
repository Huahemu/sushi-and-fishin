using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Kitchen
{
	public class SeasoningReceiver : NetworkBehaviour
	{
		private const ushort MakiSushiType = 1;

		private const ushort UramakiSushiType = 2;

		public static readonly HashSet<ushort> AllowedSushiTypes = new HashSet<ushort> { 1, 2 };

		[SerializeField]
		private Transform _visualRoot;

		[SerializeField]
		private int _maxDecalCount = 20;

		[Header("Surface Local Y")]
		[FormerlySerializedAs("_plateWorldY")]
		[SerializeField]
		private float _plateLocalY;

		[FormerlySerializedAs("_sushiWorldY")]
		[SerializeField]
		private float _sushiLocalY;

		[SerializeField]
		private float _surfaceHeightTolerance = 0.15f;

		[SerializeField]
		private float _receiverBoundsTolerance = 0.08f;

		[Header("Plate Bounds")]
		[Tooltip("Optional local XZ circular clamp for plate applications. Use it to trim square BoxCollider corners.")]
		[SerializeField]
		private Vector2 _plateLocalCenterXZ = Vector2.zero;

		[SerializeField]
		private float _plateLocalRadius;

		private readonly Dictionary<ulong, Queue<GameObject>> _spawnedDecalsBySeasoning = new Dictionary<ulong, Queue<GameObject>>();

		public bool TryCreateApplication(RaycastHit hit, out Vector3 localPosition, out SeasoningSurfaceType surfaceType)
		{
			localPosition = default;
			surfaceType = SeasoningSurfaceType.Plate;
			if (_visualRoot == null || hit.collider == null)
			{
				return false;
			}
			if (hit.collider.GetComponentInParent<SeasoningReceiver>() != this)
			{
				return false;
			}
			localPosition = _visualRoot.InverseTransformPoint(hit.point);
			surfaceType = GetSurfaceType(hit.collider);
			return true;
		}

		public SeasoningSurfaceType GetSurfaceType(Collider hitCollider)
		{
			if (hitCollider == null)
			{
				return SeasoningSurfaceType.Plate;
			}
			SushiSurfaceMarker componentInParent = hitCollider.GetComponentInParent<SushiSurfaceMarker>();
			if (!(componentInParent != null))
			{
				return SeasoningSurfaceType.Plate;
			}
			return componentInParent.SurfaceType;
		}

		public Vector3 GetSurfaceWorldPoint(Vector3 localPosition, SeasoningSurfaceType surfaceType)
		{
			localPosition.y = GetSurfaceLocalY(surfaceType);
			if (!(_visualRoot != null))
			{
				return base.transform.TransformPoint(localPosition);
			}
			return _visualRoot.TransformPoint(localPosition);
		}

		public void RequestApplySeasoning(Seasoning seasoning, Vector3 localPosition, SeasoningSurfaceType surfaceType)
		{
			if (!(seasoning == null) && base.IsSpawned)
			{
				ServerApplySeasoningRpc(seasoning.NetworkObject, localPosition, surfaceType);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerApplySeasoningRpc(NetworkObjectReference seasoningRef, Vector3 localPosition, SeasoningSurfaceType surfaceType)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3634404974u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in seasoningRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in localPosition);
				bufferWriter.WriteValueSafe(in surfaceType, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 3634404974u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (seasoningRef.TryGet(out var networkObject) && networkObject.TryGetComponent<Seasoning>(out var component) && !(component.DecalPrefab == null) && !(_visualRoot == null))
			{
				ServedDishSushi componentInParent = GetComponentInParent<ServedDishSushi>();
				if (!(componentInParent == null) && componentInParent.IsSpawned && CanApplySeasoningAt(localPosition, surfaceType) && component.ServerTryConsumeOne())
				{
					localPosition.y = GetSurfaceLocalY(surfaceType);
					float z = Random.Range(0f, 360f);
					Vector3 localEulerAngles = new Vector3(90f, 0f, z);
					componentInParent.AddSeasoningDecalData(component.ItemSO.Id, localPosition, localEulerAngles);
					ClientApplySeasoningRpc(localPosition, localEulerAngles, component.NetworkObjectId);
					PlaySeasoningSfx(component);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientApplySeasoningRpc(Vector3 localPosition, Vector3 localEulerAngles, ulong seasoningObjectId)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4122261342u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in localPosition);
				bufferWriter.WriteValueSafe(in localEulerAngles);
				BytePacker.WriteValueBitPacked(bufferWriter, seasoningObjectId);
				__endSendRpc(ref bufferWriter, 4122261342u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_visualRoot == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(seasoningObjectId, out var value) || !value.TryGetComponent<Seasoning>(out var component) || component.DecalPrefab == null)
			{
				return;
			}
			if (!_spawnedDecalsBySeasoning.TryGetValue(seasoningObjectId, out var value2))
			{
				value2 = new Queue<GameObject>();
				_spawnedDecalsBySeasoning.Add(seasoningObjectId, value2);
			}
			int num = Mathf.Max(1, _maxDecalCount);
			while (value2.Count >= num)
			{
				GameObject gameObject = value2.Dequeue();
				if (gameObject != null)
				{
					Object.Destroy(gameObject);
				}
			}
			GameObject gameObject2 = Object.Instantiate(component.DecalPrefab, _visualRoot);
			gameObject2.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(localEulerAngles));
			value2.Enqueue(gameObject2);
		}

		public bool CanApplySeasoningAt(Vector3 localPosition, SeasoningSurfaceType surfaceType)
		{
			if (_visualRoot == null)
			{
				return false;
			}
			if (surfaceType != SeasoningSurfaceType.Plate && surfaceType != SeasoningSurfaceType.Sushi)
			{
				return false;
			}
			if (!AllowsSeasoningOnThisDish())
			{
				return false;
			}
			if (surfaceType == SeasoningSurfaceType.Plate && !IsInsidePlateBounds(localPosition))
			{
				return false;
			}
			float num = Mathf.Max(0f, _surfaceHeightTolerance);
			if (num > 0f && Mathf.Abs(localPosition.y - GetSurfaceLocalY(surfaceType)) > num)
			{
				return false;
			}
			Vector3 position = localPosition;
			position.y = GetSurfaceLocalY(surfaceType);
			Vector3 vector = _visualRoot.TransformPoint(position);
			float num2 = Mathf.Max(0f, _receiverBoundsTolerance);
			float num3 = num2 * num2;
			Collider[] componentsInChildren = GetComponentsInChildren<Collider>();
			foreach (Collider collider in componentsInChildren)
			{
				if (!(collider == null) && collider.enabled && GetSurfaceType(collider) == surfaceType && (collider.ClosestPoint(vector) - vector).sqrMagnitude <= num3)
				{
					return true;
				}
			}
			return false;
		}

		private bool AllowsSeasoningOnThisDish()
		{
			ServedDishSushi componentInParent = GetComponentInParent<ServedDishSushi>();
			if (componentInParent == null)
			{
				return false;
			}
			if (!(componentInParent.ItemSO is SushiSO sushiSO))
			{
				return false;
			}
			return AllowedSushiTypes.Contains((ushort)sushiSO.SushiType);
		}

		private bool IsInsidePlateBounds(Vector3 localPosition)
		{
			float num = Mathf.Max(0f, _plateLocalRadius);
			if (num <= 0f)
			{
				return true;
			}
			return new Vector2(localPosition.x - _plateLocalCenterXZ.x, localPosition.z - _plateLocalCenterXZ.y).sqrMagnitude <= num * num;
		}

		private void PlaySeasoningSfx(Seasoning seasoning)
		{
			if (!(seasoning == null) && !(seasoning.CurrentRack == null))
			{
				seasoning.CurrentRack.PlayChefDeskSfxForActiveUser(PaperPixelGames.Core.Kitchen.ChefDesk.ChefDesk.ChefDeskSfxId.SeasoningSprinkle, 0.9f, useRandomPitch: true);
			}
		}

		private float GetSurfaceLocalY(SeasoningSurfaceType surfaceType)
		{
			if (surfaceType != SeasoningSurfaceType.Sushi)
			{
				return _plateLocalY;
			}
			return _sushiLocalY;
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			ClearSpawnedDecals();
		}

		private void ClearSpawnedDecals()
		{
			foreach (Queue<GameObject> value in _spawnedDecalsBySeasoning.Values)
			{
				while (value.Count > 0)
				{
					GameObject gameObject = value.Dequeue();
					if (gameObject != null)
					{
						Object.Destroy(gameObject);
					}
				}
			}
			_spawnedDecalsBySeasoning.Clear();
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3634404974u, __rpc_handler_3634404974, "ServerApplySeasoningRpc", RpcInvokePermission.Everyone);
			__registerRpc(4122261342u, __rpc_handler_4122261342, "ClientApplySeasoningRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3634404974(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out SeasoningSurfaceType value3, default(FastBufferWriter.ForEnums));
				((SeasoningReceiver)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SeasoningReceiver)target).ServerApplySeasoningRpc(value, value2, value3);
				((SeasoningReceiver)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4122261342(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((SeasoningReceiver)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SeasoningReceiver)target).ClientApplySeasoningRpc(value, value2, value3);
				((SeasoningReceiver)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "SeasoningReceiver";
		}
	}
}
