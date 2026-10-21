namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

public class NPCBehaviorState
{
	public enum State
	{
		Idle = 0,
		Roaming = 1,
		Watching = 2,
		Socializing = 3,
		UsingSocket = 4
	}

	public State currentState;

	public float stateUntil;

	public Vector3 targetPosition;

	public Vector3 watchPosition;

	public Transform socialTarget;

	public NPCSocket currentSocket;

	public bool hasEnteredSocket;

	public float socketStartTime;

	public float socketAttemptStartTime;

	public Dictionary<NPCSocket, float> socketCooldowns = new Dictionary<NPCSocket, float>();
}

}
