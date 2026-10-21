using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Dissonance.Integrations.Unity_NFGO.Demo
{
	public class NfgoPlayerSpawner : MonoBehaviour
	{
		public GameObject PlayerPrefab;

		private void OnEnable()
		{
			StartCoroutine(SpawnCo());
		}

		private IEnumerator SpawnCo()
		{
			NetworkManager nm = NetworkManager.Singleton;
			if (nm.IsServer)
			{
				DissonanceComms comms = null;
				while ((object)comms == null)
				{
					comms = Object.FindAnyObjectByType<DissonanceComms>();
					yield return null;
				}
				nm.OnClientConnectedCallback += Spawn;
				Spawn(nm.LocalClientId);
			}
		}

		private void Spawn(ulong owner)
		{
			Object.Instantiate(position: new Vector3(Random.Range(-15, 15), 0f, Random.Range(-15, 15)), original: PlayerPrefab, rotation: Quaternion.identity).GetComponent<NetworkObject>().SpawnAsPlayerObject(owner, destroyWithScene: true);
		}
	}
}
