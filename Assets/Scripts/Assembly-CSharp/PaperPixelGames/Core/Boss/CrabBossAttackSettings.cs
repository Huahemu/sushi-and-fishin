using System;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	[Serializable]
	public sealed class CrabBossAttackSettings
	{
		[SerializeField]
		[Tooltip("Disabled attacks stay configured but are excluded from selection and validation.")]
		private bool _enabled = true;

		[SerializeField]
		private CrabBossAttackId _id;

		[SerializeField]
		private CrabBossAttackAnimation _animation;

		[SerializeField]
		private int _unlockPhase;

		[SerializeField]
		private float _selectionWeight;

		[SerializeField]
		private float _timeout;

		[SerializeField]
		private bool _loop;

		[SerializeField]
		[Min(2f)]
		private int _repeatCount = 2;

		[SerializeField]
		[Min(0f)]
		private float _repeatInterval = 0.5f;

		public bool Enabled => _enabled;

		public CrabBossAttackId Id => _id;

		public CrabBossAttackAnimation Animation => _animation;

		public int UnlockPhase => _unlockPhase;

		public float SelectionWeight => _selectionWeight;

		public float Timeout => _timeout;

		public bool Loop => _loop;

		public int TotalExecutionCount
		{
			get
			{
				if (!_loop)
				{
					return 1;
				}
				return _repeatCount;
			}
		}

		public float RepeatInterval => _repeatInterval;
	}
}
