using UnityEngine;

namespace Threepeat
{
	[CreateAssetMenu(fileName = "AnimClipDemoInfo", menuName = "Threepeat/AnimationClip Demo Info")]
	public class AnimationClipDemoInfo : ScriptableObject
	{
		public enum IKTargetLocation
		{
			Front = 0,
			Back = 1
		}

		[Multiline]
		public string animationName = "TYPE - sprint-to-sprint";

		public AnimationClip clip;

		public AnimationClip[] preExitClips = new AnimationClip[0];

		public string animatorStateName = "";

		public Vector3 obstacleFrontEdge = Vector3.zero;

		public Vector3 obstacleBackEdge = Vector3.zero;

		public Vector3 platformFrontEdge = Vector3.zero;

		public Vector3 platformBackEdge = Vector3.zero;

		public bool doLeftHandIK;

		public Vector3 leftHandIKOffset = Vector3.zero;

		public float fullIKThreshDist = 0.25f;

		public float animationDurationOverride = -1f;

		public bool rightWall;

		[Tooltip("-1 in Y means just match obstacle height, -1 in Z means go from character Z position to obstacle front edge")]
		public Vector3 rightWallXOffHeightLength = new Vector3(1f, -1f, -1f);

		public bool disappearObstacleImmediatelyAfterClip;

		public bool doRightHandIK;

		public Vector3 rightHandIKOffset = Vector3.zero;

		public float playerZForMaxIKWeight = -1f;

		public IKTargetLocation IKTarget;

		public AnimationClip entryClip;

		public AnimationClip exitClip;

		public bool doCrossfade = true;

		public string GetClipName()
		{
			if (!(animatorStateName == ""))
			{
				return animatorStateName;
			}
			return clip.name;
		}

		public bool HasObstacle()
		{
			return obstacleFrontEdge != obstacleBackEdge;
		}

		internal bool HasPlatform()
		{
			return platformFrontEdge != platformBackEdge;
		}
	}
}
