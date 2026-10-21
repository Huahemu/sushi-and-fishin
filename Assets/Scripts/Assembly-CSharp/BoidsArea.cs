using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using SDQQ1234;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class BoidsArea : MonoBehaviour
{
	[SerializeField]
	private List<CS_BoidsVFX> _boids = new List<CS_BoidsVFX>();

	private readonly HashSet<int> _localPlayerColliderIds = new HashSet<int>();

	private void OnTriggerEnter(Collider other)
	{
		if (TryGetLocalPlayerCollider(other, out var colliderId) && _localPlayerColliderIds.Add(colliderId) && _localPlayerColliderIds.Count == 1)
		{
			SetBoidsSimulationActive(active: true);
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (TryGetLocalPlayerCollider(other, out var colliderId) && _localPlayerColliderIds.Remove(colliderId) && _localPlayerColliderIds.Count == 0)
		{
			SetBoidsSimulationActive(active: false);
		}
	}

	private void OnDisable()
	{
		_localPlayerColliderIds.Clear();
		SetBoidsSimulationActive(active: false);
	}

	private static bool TryGetLocalPlayerCollider(Collider other, out int colliderId)
	{
		colliderId = 0;
		if (other == null)
		{
			return false;
		}
		PPG_Player componentInParent = other.GetComponentInParent<PPG_Player>();
		if (componentInParent == null || !componentInParent.IsOwner)
		{
			return false;
		}
		colliderId = other.GetInstanceID();
		return true;
	}

	private void SetBoidsSimulationActive(bool active)
	{
		if (_boids == null)
		{
			return;
		}
		for (int i = 0; i < _boids.Count; i++)
		{
			CS_BoidsVFX cS_BoidsVFX = _boids[i];
			if (!(cS_BoidsVFX == null))
			{
				if (active)
				{
					cS_BoidsVFX.StartSimulation();
				}
				else
				{
					cS_BoidsVFX.StopSimulation();
				}
			}
		}
	}
}
