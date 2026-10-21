namespace SushiAndFish.Casino.Original
{
using global::System;
using global::Dissonance.Integrations.MirrorIgnorance;
using global::Mirror;
using global::UnityEngine;

[Serializable]
public class PlayerReferences
{
	public NetworkIdentity identity;

	public Transform transform;

	public Transform headTransform;

	public PlayerProfile profile;

	public PlayerController controller;

	public PlayerBuff buff;

	public PlayerOrgans organs;

	public MirrorIgnorancePlayer mirrorIgnorance;

	public PlayerCarry carry;

	public PlayerMouth mouth;

	public PlayerReferences(NetworkIdentity netIdentity)
	{
		identity = netIdentity;
		transform = netIdentity.transform;
		profile = netIdentity.GetComponent<PlayerProfile>();
		controller = netIdentity.GetComponent<PlayerController>();
		headTransform = controller.head.transform;
		buff = netIdentity.GetComponent<PlayerBuff>();
		organs = netIdentity.GetComponent<PlayerOrgans>();
		mirrorIgnorance = netIdentity.GetComponent<MirrorIgnorancePlayer>();
		carry = netIdentity.GetComponent<PlayerCarry>();
		mouth = netIdentity.GetComponent<PlayerMouth>();
	}
}

}
