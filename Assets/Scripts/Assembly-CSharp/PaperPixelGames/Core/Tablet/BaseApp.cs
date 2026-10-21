using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public class BaseApp : MonoBehaviour
	{
		public virtual void Initialize()
		{
		}

		public virtual void Show()
		{
			base.gameObject.SetActive(value: true);
		}

		public virtual void Hide()
		{
			base.gameObject.SetActive(value: false);
		}
	}
}
