namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace Smooth
{
	public class StateMirror
	{
		public float ownerTimestamp;

		public Vector3 position;

		public Quaternion rotation;

		public Vector3 scale;

		public Vector3 velocity;

		public Vector3 angularVelocity;

		public bool teleport;

		public bool atPositionalRest;

		public bool atRotationalRest;

		public float receivedOnServerTimestamp;

		public float receivedTimestamp;

		public int localTimeResetIndicator;

		public Vector3 reusableRotationVector;

		public bool serverShouldRelayPosition;

		public bool serverShouldRelayRotation;

		public bool serverShouldRelayScale;

		public bool serverShouldRelayVelocity;

		public bool serverShouldRelayAngularVelocity;

		public StateMirror copyFromState(StateMirror state)
		{
			ownerTimestamp = state.ownerTimestamp;
			position = state.position;
			rotation = state.rotation;
			scale = state.scale;
			velocity = state.velocity;
			angularVelocity = state.angularVelocity;
			receivedTimestamp = state.receivedTimestamp;
			localTimeResetIndicator = state.localTimeResetIndicator;
			return this;
		}

		public static StateMirror Lerp(StateMirror targetTempStateMirror, StateMirror start, StateMirror end, float t)
		{
			targetTempStateMirror.position = Vector3.Lerp(start.position, end.position, t);
			targetTempStateMirror.rotation = Quaternion.Lerp(start.rotation, end.rotation, t);
			targetTempStateMirror.scale = Vector3.Lerp(start.scale, end.scale, t);
			targetTempStateMirror.velocity = Vector3.Lerp(start.velocity, end.velocity, t);
			targetTempStateMirror.angularVelocity = Vector3.Lerp(start.angularVelocity, end.angularVelocity, t);
			targetTempStateMirror.ownerTimestamp = Mathf.Lerp(start.ownerTimestamp, end.ownerTimestamp, t);
			return targetTempStateMirror;
		}

		public void resetTheVariables()
		{
			ownerTimestamp = 0f;
			position = Vector3.zero;
			rotation = Quaternion.identity;
			scale = Vector3.zero;
			velocity = Vector3.zero;
			angularVelocity = Vector3.zero;
			atPositionalRest = false;
			atRotationalRest = false;
			teleport = false;
			receivedTimestamp = 0f;
			localTimeResetIndicator = 0;
		}

		public void copyFromSmoothSync(SmoothSyncMirror smoothSyncScript)
		{
			ownerTimestamp = smoothSyncScript.localTime;
			position = smoothSyncScript.getPosition();
			rotation = smoothSyncScript.getRotation();
			scale = smoothSyncScript.getScale();
			if (smoothSyncScript.hasRigidbody)
			{
				velocity = smoothSyncScript.rb.linearVelocity;
				angularVelocity = smoothSyncScript.rb.angularVelocity * 57.29578f;
			}
			else if (smoothSyncScript.hasRigidbody2D)
			{
				velocity = smoothSyncScript.rb2D.linearVelocity;
				angularVelocity.x = 0f;
				angularVelocity.y = 0f;
				angularVelocity.z = smoothSyncScript.rb2D.angularVelocity;
			}
			else
			{
				velocity = Vector3.zero;
				angularVelocity = Vector3.zero;
			}
			localTimeResetIndicator = smoothSyncScript.localTimeResetIndicator;
		}
	}
}

}
