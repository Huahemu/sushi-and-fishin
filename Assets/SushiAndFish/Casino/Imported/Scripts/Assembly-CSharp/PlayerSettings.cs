namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Player Settings", fileName = "PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
	public float playerRadius;

	public float playerHeadRadius;

	public float headHeight;

	public float headHeightCrouch;

	[Header("Player Movement")]
	public float maxSpeed;

	public float acceleration;

	public float sprintMaxSpeed;

	public float crouchMaxSpeed;

	public float rollMaxSpeed;

	public float rollAcceleration;

	public float jumpForce;

	public float gravity;

	public float groundCheckDistance;

	public float maxSlopeAngle;

	public float maxStepHeight;

	public float stepCheckDistance;

	public float stepUpDistance;

	public LayerMask groundMask;

	public float landThreshold;

	public float ragdollDuration;

	public float headMoveDuration;

	[Header("Item Settings")]
	public float minItemThrowForce;

	public float maxItemThrowForce;

	public float minItemThrowTorque;

	public float maxItemThrowTorque;

	public float itemThrowDuration;

	public float throwThreshold;

	public float constantMass;

	public static event Action<PlayerSettings> SettingsChanged;

	public void NotifyChanged()
	{
		SettingsChanged?.Invoke(this);
	}
}

}
