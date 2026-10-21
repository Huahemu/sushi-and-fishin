namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class NPCSocket : MonoBehaviour
{
	[Header("Socket Settings")]
	[SerializeField]
	private NPCSocketAction action;

	[SerializeField]
	private Transform socketPosition;

	[SerializeField]
	private float useRadius = 1f;

	[SerializeField]
	private int maxNPCs = 1;

	public NPCSocketAction Action => action;

	public Vector3 Position
	{
		get
		{
			if (!(socketPosition != null))
			{
				return base.transform.position;
			}
			return socketPosition.position;
		}
	}

	public Vector3 Forward
	{
		get
		{
			if (!(socketPosition != null))
			{
				return base.transform.forward;
			}
			return socketPosition.forward;
		}
	}

	public float UseRadius => useRadius;

	public int MaxNPCs => maxNPCs;

	public int CurrentUsers { get; private set; }

	private void Awake()
	{
		if (socketPosition == null)
		{
			socketPosition = base.transform;
		}
		if (MonoSingleton<NPCSocketManager>.Instance != null)
		{
			MonoSingleton<NPCSocketManager>.Instance.RegisterSocket(this);
		}
	}

	private void OnDestroy()
	{
		if (MonoSingleton<NPCSocketManager>.Instance != null)
		{
			MonoSingleton<NPCSocketManager>.Instance.UnregisterSocket(this);
		}
	}

	public bool IsAvailable()
	{
		return CurrentUsers < maxNPCs;
	}

	public void Reserve()
	{
		CurrentUsers++;
	}

	public void Release()
	{
		CurrentUsers = Mathf.Max(0, CurrentUsers - 1);
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(Position, useRadius);
	}
}

}
