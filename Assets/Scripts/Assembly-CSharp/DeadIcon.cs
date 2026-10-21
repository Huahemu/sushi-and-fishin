using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

public class DeadIcon : MonoBehaviour
{
	private void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player") && other.TryGetComponent<PPG_Player>(out var component) && component == Singleton<PlayersManager>.Instance.LocalPlayer)
		{
			Object.Destroy(base.gameObject);
		}
	}
}
