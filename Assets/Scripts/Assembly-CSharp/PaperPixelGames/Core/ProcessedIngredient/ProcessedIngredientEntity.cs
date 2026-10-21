using System.Collections;
using PaperPixelGames.Framework.Indicator;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.ProcessedIngredient
{
	public class ProcessedIngredientEntity : IngredientEntity
	{
		[SerializeField]
		private ParticleSystem _smokePS;

		[SerializeField]
		[Tooltip("Restarts the world smoke effect after a physics throw settles.")]
		private bool _restartSmokeAfterPhysicsThrow;

		private float _smokeDuration = 1500f;

		private ParticleSystem _fpsParticle;

		private ParticleSystem _tpsParticle;

		private const float SMOKE_DECREASE_RATE = 1f;

		public override bool TryGetScanIndicatorVisualSettings(out OffscreenIndicatorVisualSettings settings)
		{
			settings = new OffscreenIndicatorVisualSettings
			{
				IconSprite = ((base.ItemSO != null) ? base.ItemSO.Sprite : null),
				ApplyDistanceVisible = true,
				DistanceVisible = false
			};
			return true;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (!(_smokePS == null))
			{
				StartSmokeEffect(_smokePS);
				StartCoroutine(SmokeDurationDecreaseLoop());
			}
		}

		private IEnumerator SmokeDurationDecreaseLoop()
		{
			while (_smokeDuration > 0f)
			{
				yield return new WaitForSeconds(1f);
				_smokeDuration--;
			}
			_smokeDuration = 0f;
			StopAllParticles();
		}

		protected override void SpawnFpsShadow(Transform parent)
		{
			base.SpawnFpsShadow(parent);
			if (!(_smokePS == null))
			{
				if (_fpsParticle == null)
				{
					_fpsParticle = parent.GetComponentInChildren<ParticleSystem>(includeInactive: true);
				}
				if (_fpsParticle != null)
				{
					StartSmokeEffect(_fpsParticle);
				}
			}
		}

		protected override void SpawnTpsShadow(Transform parent, bool isOwner)
		{
			base.SpawnTpsShadow(parent, isOwner);
			if (!(_smokePS == null))
			{
				if (_tpsParticle == null)
				{
					_tpsParticle = parent.GetComponentInChildren<ParticleSystem>(includeInactive: true);
				}
				if (_tpsParticle != null)
				{
					StartSmokeEffect(_tpsParticle);
				}
			}
		}

		protected override void FpsShadowStatus(bool status)
		{
			base.FpsShadowStatus(status);
			if (!(_smokePS == null) && status && _fpsParticle != null)
			{
				StartSmokeEffect(_fpsParticle);
			}
		}

		protected override void TpsShadowStatus(bool status)
		{
			base.TpsShadowStatus(status);
			if (!(_smokePS == null) && status && _tpsParticle != null)
			{
				StartSmokeEffect(_tpsParticle);
			}
		}

		protected override void ServerPlace(Vector3 pos, Vector3 rot, bool forget)
		{
			base.ServerPlace(pos, rot, forget);
			if (!(_smokePS == null))
			{
				ClientStartSmokeRpc();
			}
		}

		protected override void ClientOnPhysicsThrowEnded()
		{
			base.ClientOnPhysicsThrowEnded();
			if (_restartSmokeAfterPhysicsThrow)
			{
				StartSmokeEffect(_smokePS);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStartSmokeRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1274791709u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1274791709u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StartSmokeEffect(_smokePS);
			}
		}

		private void StartSmokeEffect(ParticleSystem ps)
		{
			if (!(ps == null) && !ps.isPlaying && _smokeDuration > 0f)
			{
				ps.Play();
			}
		}

		private void StopAllParticles()
		{
			if (_smokePS.isPlaying)
			{
				_smokePS.Stop();
			}
			if (_fpsParticle != null && _fpsParticle.isPlaying)
			{
				_fpsParticle.Stop();
			}
			if (_tpsParticle != null && _tpsParticle.isPlaying)
			{
				_tpsParticle.Stop();
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1274791709u, __rpc_handler_1274791709, "ClientStartSmokeRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1274791709(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ProcessedIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ProcessedIngredientEntity)target).ClientStartSmokeRpc();
				((ProcessedIngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ProcessedIngredientEntity";
		}
	}
}
