namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class SFXAmbienceManager : MonoBehaviour
{
	public static SFXAmbienceManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
	}
}

}
