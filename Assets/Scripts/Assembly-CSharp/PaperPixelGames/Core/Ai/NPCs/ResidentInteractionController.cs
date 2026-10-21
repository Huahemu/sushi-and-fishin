using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentInteractionController
	{
		private readonly ResidentRuntimeState _runtime;

		private readonly float _timeoutSeconds;

		private float _lockStartedAt;

		public const ulong NoInteractorClientId = ulong.MaxValue;

		public bool CanInteract => CanInteractWith(requireWorking: true);

		public bool CanGaze
		{
			get
			{
				if (CanInteract)
				{
					return !_runtime.IsInteracting;
				}
				return false;
			}
		}

		public ResidentInteractionController(ResidentRuntimeState runtime, float timeoutSeconds = 120f)
		{
			_runtime = runtime;
			_timeoutSeconds = timeoutSeconds;
		}

		public bool CanInteractWith(bool requireWorking)
		{
			if (_runtime != null && _runtime.SystemState == ResidentSystemState.Normal && _runtime.HasArrived && (!requireWorking || _runtime.IsWorking) && !_runtime.IsInRestaurant && !_runtime.IsSwimming && !_runtime.HasRestaurantAssignment)
			{
				return !_runtime.IsSnapshotApplying;
			}
			return false;
		}

		public bool TryBegin(ulong clientId, bool requireWorking = true)
		{
			if (!CanInteractWith(requireWorking))
			{
				return false;
			}
			if (_runtime.ActiveInteractorClientId != ulong.MaxValue && _runtime.ActiveInteractorClientId != clientId)
			{
				return false;
			}
			_runtime.IsInteracting = true;
			_runtime.ActiveInteractorClientId = clientId;
			_lockStartedAt = Time.time;
			return true;
		}

		public bool End(ulong clientId)
		{
			if (_runtime.ActiveInteractorClientId != ulong.MaxValue && _runtime.ActiveInteractorClientId != clientId)
			{
				return false;
			}
			Clear();
			return true;
		}

		public bool IsHeldBy(ulong clientId)
		{
			if (_runtime != null)
			{
				return _runtime.ActiveInteractorClientId == clientId;
			}
			return false;
		}

		public void Tick(float now)
		{
			if (_runtime != null && _runtime.IsInteracting && !(_timeoutSeconds <= 0f) && !(now - _lockStartedAt < _timeoutSeconds))
			{
				Clear();
			}
		}

		public void Clear()
		{
			if (_runtime != null)
			{
				_runtime.IsInteracting = false;
				_runtime.ActiveInteractorClientId = ulong.MaxValue;
			}
		}
	}
}
