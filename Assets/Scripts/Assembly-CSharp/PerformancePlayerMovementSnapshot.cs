using System;

[Serializable]
public sealed class PerformancePlayerMovementSnapshot
{
	public bool hasLocalPlayer;

	public string playerName;

	public float playerX;

	public float playerY;

	public float playerZ;

	public float inputX;

	public float inputY;

	public float inputMagnitude;

	public float speed;

	public float velocityX;

	public float velocityY;

	public float velocityZ;

	public bool isMovingInput;

	public bool isNetworkSwimming;

	public bool isCharacterSwimming;

	public bool isSeaScooter;

	public bool isUnderWater;

	public bool isGrounded;

	public string movementMode;

	public string seaLayer;
}
