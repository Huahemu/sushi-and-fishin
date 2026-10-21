using Unity.Netcode;
using UnityEngine;

namespace ECM2.Examples.Networking.Netcode
{
	public class BootstrapManager : MonoBehaviour
	{
		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(10f, 10f, 300f, 300f));
			NetworkManager singleton = NetworkManager.Singleton;
			if (!singleton.IsClient && !singleton.IsServer)
			{
				if (GUILayout.Button("Host"))
				{
					singleton.StartHost();
				}
				if (GUILayout.Button("Client"))
				{
					singleton.StartClient();
				}
				if (GUILayout.Button("Server"))
				{
					singleton.StartServer();
				}
			}
			else
			{
				GUILayout.Label("Mode: " + (singleton.IsHost ? "Host" : (singleton.IsServer ? "Server" : "Client")));
			}
			GUILayout.EndArea();
		}
	}
}
