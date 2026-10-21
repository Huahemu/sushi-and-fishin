namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class NPCSocketManager : MonoSingleton<NPCSocketManager>
{
	private List<NPCSocket> allSockets = new List<NPCSocket>();

	public void RegisterSocket(NPCSocket socket)
	{
		if (socket != null && !allSockets.Contains(socket))
		{
			allSockets.Add(socket);
		}
	}

	public void UnregisterSocket(NPCSocket socket)
	{
		if (socket != null)
		{
			allSockets.Remove(socket);
		}
	}

	public NPCSocket FindAvailableSocket(Vector3 npcPosition, float searchRadius, NPCSocketAction preferredAction = null)
	{
		NPCSocket result = null;
		float num = float.MinValue;
		foreach (NPCSocket allSocket in allSockets)
		{
			if (allSocket == null || !allSocket.gameObject.activeInHierarchy || !allSocket.IsAvailable())
			{
				continue;
			}
			float num2 = Vector3.Distance(npcPosition, allSocket.Position);
			if (!(num2 > searchRadius))
			{
				float num3 = 1f / (num2 + 1f);
				if (preferredAction != null && allSocket.Action == preferredAction)
				{
					num3 *= 2f;
				}
				if (num3 > num)
				{
					num = num3;
					result = allSocket;
				}
			}
		}
		return result;
	}

	public List<NPCSocket> GetSocketsByAction(NPCSocketAction action)
	{
		return allSockets.Where((NPCSocket s) => s != null && s.gameObject.activeInHierarchy && s.Action == action).ToList();
	}
}

}
