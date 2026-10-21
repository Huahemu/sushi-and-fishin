using System;
using System.Collections.Generic;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant.Dirt
{
	public class DirtManager : NetworkBehaviour
	{
		private struct DirtState
		{
			public byte Id;

			public Vector3 Position;

			public Quaternion Rotation;
		}

		[Header("Spawn Sources")]
		[SerializeField]
		private List<Collider> _spawnVolumes = new List<Collider>();

		[Header("Dirt Prefab")]
		[SerializeField]
		private Dirt _dirtPrefab;

		[Header("Placement")]
		[Tooltip("How far above the volume bounds to start the down raycast.")]
		[SerializeField]
		[Min(0.01f)]
		private float _raycastHeight = 10f;

		[Tooltip("Maximum distance for the down raycast.")]
		[SerializeField]
		[Min(0.1f)]
		private float _raycastDistance = 50f;

		[Tooltip("If set, dirt will only be placed on these layers.")]
		[SerializeField]
		private LayerMask _groundMask = -1;

		[Tooltip("Extra offset applied upwards after placing on the ground.")]
		[SerializeField]
		private float _surfaceOffset = 0.07f;

		[Header("Validation")]
		[Tooltip("How many attempts to find a valid point before giving up.")]
		[SerializeField]
		[Range(1f, 200f)]
		private int _maxAttempts = 40;

		[Tooltip("Reject placements if the surface normal is too steep.")]
		[SerializeField]
		[Range(0f, 89f)]
		private float _maxSlopeAngle = 35f;

		[Header("Debug")]
		[SerializeField]
		private bool _debugDraw;

		private readonly List<Dirt> _spawned = new List<Dirt>();

		private byte _nextDirtId = 1;

		private readonly Dictionary<byte, Dirt> _dirtById = new Dictionary<byte, Dirt>();

		private readonly Dictionary<byte, DirtState> _activeDirtStates = new Dictionary<byte, DirtState>();

		public override void OnNetworkSpawn()
		{
			_ = base.IsServer;
		}

		[Rpc(SendTo.Server)]
		public void ServerSpawnBatchRpc(int count)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2389046829u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, count);
				__endSendRpc(ref bufferWriter, 2389046829u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (count > 0)
			{
				for (int i = 0; i < count; i++)
				{
					ServerTrySpawnDirtRpc();
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerTrySpawnDirtRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(4109759597u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4109759597u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_dirtPrefab == null)
			{
				UtilityMethods.Log("[DirtManager - ServerTrySpawnDirtRpc] Dirt prefab is null. Spawn aborted.");
				return;
			}
			if (_nextDirtId == 0)
			{
				UtilityMethods.Log("[DirtManager - ServerTrySpawnDirtRpc] Dirt ID limit reached. No more dirt will be spawned.");
				return;
			}
			Collider collider = PickRandomValidVolume();
			if (collider == null)
			{
				UtilityMethods.Log("[DirtManager - ServerTrySpawnDirtRpc] No valid spawn volume. Spawn aborted.");
				return;
			}
			if (!TryGetRandomPointInsideVolume(collider, out var hitPoint, out var hitNormal))
			{
				UtilityMethods.Log("[DirtManager - ServerTrySpawnDirtRpc] Failed to find a valid point in spawn volume. Spawn aborted.");
				return;
			}
			byte b = _nextDirtId++;
			Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);
			_activeDirtStates[b] = new DirtState
			{
				Id = b,
				Position = hitPoint,
				Rotation = rotation
			};
			ClientSpawnDirtRpc(hitPoint, rotation, b);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSpawnDirtRpc(Vector3 spawnPoint, Quaternion rotation, byte id)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1285324692u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in spawnPoint);
				bufferWriter.WriteValueSafe(in rotation);
				bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1285324692u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!_dirtById.ContainsKey(id))
				{
					Dirt dirt = UnityEngine.Object.Instantiate(_dirtPrefab, spawnPoint, rotation);
					dirt.Initialize(id, this);
					_spawned.Add(dirt);
					_dirtById.Add(id, dirt);
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerDirtDestroyedRpc(byte dirtID)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2115852437u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in dirtID, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2115852437u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_activeDirtStates.ContainsKey(dirtID))
				{
					_activeDirtStates.Remove(dirtID);
					ClientDestroyDirtRpc(dirtID);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientDestroyDirtRpc(byte dirtID)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2684743124u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in dirtID, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2684743124u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_dirtById.TryGetValue(dirtID, out var value))
				{
					_dirtById.Remove(dirtID);
					_spawned.Remove(value);
					UnityEngine.Object.Destroy(value.gameObject);
				}
			}
		}

		private void OnClientConnected(ulong clientId)
		{
			if (!base.IsServer || _activeDirtStates.Count == 0)
			{
				return;
			}
			int count = _activeDirtStates.Count;
			byte[] array = new byte[count];
			Vector3[] array2 = new Vector3[count];
			Quaternion[] array3 = new Quaternion[count];
			int num = 0;
			foreach (KeyValuePair<byte, DirtState> activeDirtState in _activeDirtStates)
			{
				DirtState value = activeDirtState.Value;
				array[num] = value.Id;
				array2[num] = value.Position;
				array3[num] = value.Rotation;
				num++;
			}
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(clientId, RpcTargetUse.Temp);
			ClientSyncAllDirtRpc(array, array2, array3, baseRpcTarget);
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientSyncAllDirtRpc(byte[] ids, Vector3[] positions, Quaternion[] rotations, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3818685541u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = ids != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(ids, default(FastBufferWriter.ForPrimitives));
				}
				bool value2 = positions != null;
				bufferWriter.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
				if (value2)
				{
					bufferWriter.WriteValueSafe(positions);
				}
				bool value3 = rotations != null;
				bufferWriter.WriteValueSafe(in value3, default(FastBufferWriter.ForPrimitives));
				if (value3)
				{
					bufferWriter.WriteValueSafe(rotations);
				}
				__endSendRpc(ref bufferWriter, 3818685541u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (ids == null || positions == null || rotations == null)
			{
				return;
			}
			int num = ids.Length;
			if (positions.Length != num || rotations.Length != num)
			{
				return;
			}
			for (int i = 0; i < num; i++)
			{
				byte b = ids[i];
				if (!_dirtById.ContainsKey(b))
				{
					Dirt dirt = UnityEngine.Object.Instantiate(_dirtPrefab, positions[i], rotations[i]);
					dirt.Initialize(b, this);
					_spawned.Add(dirt);
					_dirtById.Add(b, dirt);
				}
			}
		}

		private Collider PickRandomValidVolume()
		{
			if (_spawnVolumes == null || _spawnVolumes.Count == 0)
			{
				return null;
			}
			for (int i = 0; i < 10; i++)
			{
				Collider collider = _spawnVolumes[UnityEngine.Random.Range(0, _spawnVolumes.Count)];
				if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
				{
					return collider;
				}
			}
			foreach (Collider spawnVolume in _spawnVolumes)
			{
				if (spawnVolume != null && spawnVolume.enabled && spawnVolume.gameObject.activeInHierarchy)
				{
					return spawnVolume;
				}
			}
			return null;
		}

		private bool TryGetRandomPointInsideVolume(Collider volume, out Vector3 hitPoint, out Vector3 hitNormal)
		{
			hitPoint = default;
			hitNormal = Vector3.up;
			if (volume == null)
			{
				return false;
			}
			Bounds bounds = volume.bounds;
			float num = Mathf.Cos(_maxSlopeAngle * (MathF.PI / 180f));
			for (int i = 0; i < _maxAttempts; i++)
			{
				float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
				float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
				Vector3 vector = new Vector3(x, bounds.max.y + _raycastHeight, z);
				if (!Physics.Raycast(new Ray(vector, Vector3.down), out var hitInfo, _raycastDistance, _groundMask, QueryTriggerInteraction.Ignore))
				{
					continue;
				}
				Vector3 point = hitInfo.point;
				if ((volume.ClosestPoint(point) - point).sqrMagnitude <= 0.0001f && !(Vector3.Dot(hitInfo.normal, Vector3.up) < num))
				{
					hitPoint = point + Vector3.up * _surfaceOffset;
					hitNormal = hitInfo.normal;
					if (_debugDraw)
					{
						Debug.DrawLine(vector, hitInfo.point, Color.green, 2f);
						Debug.DrawRay(hitInfo.point, hitInfo.normal, Color.cyan, 2f);
					}
					return true;
				}
			}
			return false;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2389046829u, __rpc_handler_2389046829, "ServerSpawnBatchRpc", RpcInvokePermission.Everyone);
			__registerRpc(4109759597u, __rpc_handler_4109759597, "ServerTrySpawnDirtRpc", RpcInvokePermission.Everyone);
			__registerRpc(1285324692u, __rpc_handler_1285324692, "ClientSpawnDirtRpc", RpcInvokePermission.Everyone);
			__registerRpc(2115852437u, __rpc_handler_2115852437, "ServerDirtDestroyedRpc", RpcInvokePermission.Everyone);
			__registerRpc(2684743124u, __rpc_handler_2684743124, "ClientDestroyDirtRpc", RpcInvokePermission.Everyone);
			__registerRpc(3818685541u, __rpc_handler_3818685541, "ClientSyncAllDirtRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2389046829(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DirtManager)target).ServerSpawnBatchRpc(value);
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4109759597(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DirtManager)target).ServerTrySpawnDirtRpc();
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1285324692(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Quaternion value2);
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DirtManager)target).ClientSpawnDirtRpc(value, value2, value3);
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2115852437(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DirtManager)target).ServerDirtDestroyedRpc(value);
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2684743124(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DirtManager)target).ClientDestroyDirtRpc(value);
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3818685541(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				byte[] value2 = null;
				if (value)
				{
					reader.ReadValueSafe(out value2, default(FastBufferWriter.ForPrimitives));
				}
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				Vector3[] value4 = null;
				if (value3)
				{
					reader.ReadValueSafe(out value4);
				}
				reader.ReadValueSafe(out bool value5, default(FastBufferWriter.ForPrimitives));
				Quaternion[] value6 = null;
				if (value5)
				{
					reader.ReadValueSafe(out value6);
				}
				RpcParams ext = rpcParams.Ext;
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DirtManager)target).ClientSyncAllDirtRpc(value2, value4, value6, ext);
				((DirtManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "DirtManager";
		}
	}
}
