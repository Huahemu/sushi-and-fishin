using System.Collections.Generic;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

public class SillyPufferManager : MonoBehaviour
{
	[SerializeField]
	private List<SillyPuffer> _sillyPuffers;

	public void PlayMovementPuffers()
	{
		Singleton<DelayHelper>.Instance.DelayAction(5f, () =>
		{
			foreach (SillyPuffer sillyPuffer in _sillyPuffers)
			{
				sillyPuffer.PlayMovement();
			}
		});
	}

	public void ResetMovementPuffers()
	{
		foreach (SillyPuffer sillyPuffer in _sillyPuffers)
		{
			sillyPuffer.ResetMovement();
		}
	}

	public void ResetPuffers()
	{
		foreach (SillyPuffer sillyPuffer in _sillyPuffers)
		{
			sillyPuffer.ResetPuffer();
		}
	}
}
