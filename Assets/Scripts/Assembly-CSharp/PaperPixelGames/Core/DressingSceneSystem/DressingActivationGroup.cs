using UnityEngine;

namespace PaperPixelGames.Core.DressingSceneSystem
{
	public class DressingActivationGroup : MonoBehaviour
	{
		[SerializeField]
		[Min(1f)]
		private int _activationWeight = 1;

		[SerializeField]
		private bool _activateOnPrepare;

		public int ActivationWeight => Mathf.Max(1, _activationWeight);

		public bool ActivateOnPrepare => _activateOnPrepare;

		public bool IsActivated => base.gameObject.activeSelf;

		public void SetActivated(bool isActive)
		{
			if (base.gameObject.activeSelf != isActive)
			{
				base.gameObject.SetActive(isActive);
			}
		}
	}
}
