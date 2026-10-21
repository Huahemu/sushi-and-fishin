using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.DressingSceneSystem
{
	public class DressingSceneRoot : MonoBehaviour
	{
		[SerializeField]
		private List<DressingActivationGroup> _groups = new List<DressingActivationGroup>();

		public IReadOnlyList<DressingActivationGroup> Groups => _groups;

		public void PrepareForStreaming()
		{
			for (int i = 0; i < _groups.Count; i++)
			{
				DressingActivationGroup dressingActivationGroup = _groups[i];
				if (!(dressingActivationGroup == null))
				{
					dressingActivationGroup.SetActivated(dressingActivationGroup.ActivateOnPrepare);
				}
			}
		}

		public void ActivateAllImmediate()
		{
			for (int i = 0; i < _groups.Count; i++)
			{
				DressingActivationGroup dressingActivationGroup = _groups[i];
				if (!(dressingActivationGroup == null))
				{
					dressingActivationGroup.SetActivated(isActive: true);
				}
			}
		}
	}
}
