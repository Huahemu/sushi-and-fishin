using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class SharkAttack03Hit : MonoBehaviour
	{
		[Header("Attack Routing")]
		[SerializeField]
		private SharkCreature _shark;

		[Header("Tail Hit")]
		[SerializeField]
		private Transform _castStart;

		[SerializeField]
		private Transform _castEnd;

		[SerializeField]
		[Min(0.01f)]
		private float _radius = 2.5f;

		[SerializeField]
		private LayerMask _hitMask;

		[Header("Debug")]
		[SerializeField]
		private bool _debugLogs = true;

		private readonly Collider[] _hitBuffer = new Collider[64];

		public void Anim_DoAttack03Hit()
		{
			if (_shark == null)
			{
				UtilityMethods.Log("[SharkAttack03Hit] Shark reference is missing.", LogType.Warning);
			}
			else if (!_shark.CanProcessAttack03Hit)
			{
				Log("Blocked. Shark cannot process Attack03 hit.");
			}
			else
			{
				PerformTailHitCheck();
			}
		}

		private void PerformTailHitCheck()
		{
			if (_castStart == null || _castEnd == null)
			{
				UtilityMethods.Log("[SharkAttack03Hit] Tail cast transforms are missing.", LogType.Warning);
				return;
			}
			int num = Physics.OverlapCapsuleNonAlloc(_castStart.position, _castEnd.position, _radius, _hitBuffer, _hitMask, QueryTriggerInteraction.Collide);
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				Collider collider = _hitBuffer[i];
				if (!(collider == null) && collider.CompareTag("Player") && _shark.ServerTryReceiveTailHit(collider))
				{
					num2++;
				}
			}
			Log((num2 > 0) ? $"Pushed players={num2}" : "Miss. No eligible Player collider resolved.");
		}

		private void Log(string message)
		{
			if (_debugLogs)
			{
				UtilityMethods.Log("[SharkAttack03Hit] " + message);
			}
		}
	}
}
