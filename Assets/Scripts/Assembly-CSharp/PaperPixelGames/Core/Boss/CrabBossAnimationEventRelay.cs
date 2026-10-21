using PaperPixelGames.Core.Boss.Attacks;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	public sealed class CrabBossAnimationEventRelay : MonoBehaviour
	{
		[SerializeField]
		private CrabBossGrabThrowAttack _grabThrowAttack;

		[SerializeField]
		private CrabBossPresentationController _presentation;

		[SerializeField]
		private CrabBossStalactiteDropAttack _stalactiteDropAttack;

		[SerializeField]
		private CrabBossCreatureSpawnAttack _creatureSpawnAttack;

		[SerializeField]
		private CrabBossProjectTileAttack _projectTileAttack;

		private void Awake()
		{
			if (_grabThrowAttack == null)
			{
				_grabThrowAttack = GetComponentInParent<CrabBossGrabThrowAttack>();
			}
			if (_presentation == null)
			{
				_presentation = GetComponentInParent<CrabBossPresentationController>();
			}
			if (_stalactiteDropAttack == null)
			{
				_stalactiteDropAttack = GetComponentInParent<CrabBossStalactiteDropAttack>();
			}
			if (_creatureSpawnAttack == null)
			{
				_creatureSpawnAttack = GetComponentInParent<CrabBossCreatureSpawnAttack>();
			}
			if (_projectTileAttack == null)
			{
				_projectTileAttack = GetComponentInParent<CrabBossProjectTileAttack>();
			}
		}

		public void ThrowPlayer()
		{
			_presentation?.PlayThrowPlayerSfx();
			_grabThrowAttack?.AnimationEventThrowPlayer();
		}

		public void GrabPlayer()
		{
			_presentation?.PlayGrabPlayerSfx();
			_grabThrowAttack?.AnimationEventGrabPlayer();
		}

		public void PlayStalactiteDropLandingPresentation()
		{
			_presentation?.PlayStalactiteDropLandingPresentation();
			_stalactiteDropAttack?.AnimationEventStartFallingRocks();
		}

		public void SpawnCreature()
		{
			_presentation?.PlayCreatureSpawnRoarPresentation();
			_creatureSpawnAttack?.AnimationEventSpawnCreature();
		}

		public void ThrowProjectTile()
		{
			_presentation?.PlayProjectTileLaunchPresentation();
			_projectTileAttack?.AnimationEventThrowProjectTile();
		}

		public void ProjectTileAnimationFinished()
		{
			_projectTileAttack?.AnimationEventProjectTileFinished();
		}
	}
}
