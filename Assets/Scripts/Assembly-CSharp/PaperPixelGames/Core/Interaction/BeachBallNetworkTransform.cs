using System;
using Unity.Netcode.Components;
using UnityEngine;

namespace PaperPixelGames.Core.Interaction
{
	public sealed class BeachBallNetworkTransform : NetworkTransform
	{
		public event Action<Vector3, Quaternion> AuthoritativeTeleportReceived;

		protected override void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState, ref NetworkTransformState newState)
		{
			base.OnNetworkTransformStateUpdated(ref oldState, ref newState);
			if (!base.CanCommitToTransform && newState.IsTeleportingNextFrame)
			{
				AuthoritativeTeleportReceived?.Invoke(newState.GetPosition(), newState.GetRotation());
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "BeachBallNetworkTransform";
		}
	}
}
