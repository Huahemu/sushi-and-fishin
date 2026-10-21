using System.Collections;
using TMPro;
using UnityEngine;

namespace Threepeat
{
	public class AnimatorSequential : MonoBehaviour
	{
		private Animator animator;

		public AnimationClipDemoInfo[] clipInfos = new AnimationClipDemoInfo[0];

		public GameObject obstacleObject;

		public GameObject platformObject;

		public GameObject rightwallObject;

		public bool autoplayOnRun = true;

		public string stateToPlayOnStop = "RunFwdStop";

		public TextMeshProUGUI textObject;

		private float goalZ;

		private Vector3 handGoalPos;

		private bool doingIK;

		private AnimationClipDemoInfo currentClip;

		private bool currentlyPlaying;

		private void Start()
		{
			animator = GetComponent<Animator>();
		}

		private void Update()
		{
			if (autoplayOnRun && !currentlyPlaying)
			{
				StartCoroutine(PlayClips());
				currentlyPlaying = true;
			}
			if (Input.GetKeyDown(KeyCode.Alpha1))
			{
				StartCoroutine(PlayClip(0, !Input.GetKey(KeyCode.LeftShift), !Input.GetKey(KeyCode.RightShift)));
			}
			if (Input.GetKeyDown(KeyCode.Alpha2))
			{
				StartCoroutine(PlayClip(1, !Input.GetKey(KeyCode.LeftShift), crossfade: true));
			}
			if (Input.GetKeyDown(KeyCode.Space) && !currentlyPlaying)
			{
				StartCoroutine(PlayClips());
			}
			if (Input.GetKeyDown(KeyCode.Alpha0))
			{
				base.transform.position = Vector3.zero;
				base.transform.rotation = Quaternion.LookRotation(Vector3.forward);
			}
			if (Input.GetKeyDown(KeyCode.Alpha9))
			{
				if (Time.timeScale != 1f)
				{
					Time.timeScale = 1f;
				}
				else
				{
					Time.timeScale = 0.25f;
				}
			}
		}

		public IEnumerator PlayClips()
		{
			for (int ii = 0; ii < clipInfos.Length; ii++)
			{
				yield return PlayClip(ii, playIdleOnComplete: false, clipInfos[ii].doCrossfade);
			}
			animator.CrossFadeInFixedTime(stateToPlayOnStop, 0.25f);
			currentlyPlaying = false;
			yield return null;
		}

		private IEnumerator MakeObjectDisappear(GameObject obj, float duration, bool sendPlayerBackToGround = false, float delayStart = 0f)
		{
			if (delayStart > 0f)
			{
				yield return new WaitForSeconds(delayStart);
			}
			Vector3 startPos = obj.transform.position;
			float y = obj.transform.localScale.y;
			Vector3 endPos = new Vector3(startPos.x, 0f - y, startPos.z);
			float t = 0f;
			float playerStartY = base.transform.position.y;
			while (t < 1f)
			{
				obj.transform.position = Vector3.Lerp(startPos, endPos, t);
				if (sendPlayerBackToGround)
				{
					base.transform.position = new Vector3(base.transform.position.x, Mathf.Lerp(playerStartY, 0f, t), base.transform.position.z);
				}
				t += Time.deltaTime / duration;
				yield return null;
			}
			obj.transform.position = endPos;
			yield return 0;
		}

		private IEnumerator MakeObjectAppear(GameObject obj, Vector3 finalPosition, float duration)
		{
			Vector3 startPos = obj.transform.position;
			float t = 0f;
			while (t < 1f)
			{
				obj.transform.position = Vector3.Lerp(startPos, finalPosition, t);
				t += Time.deltaTime / duration;
				yield return null;
			}
			obj.transform.position = finalPosition;
			yield return 0;
		}

		private IEnumerator MakeObjectAndCharacterAppear(GameObject obj, Vector3 finalPosition, float playerYOffset, float duration, bool moveCharacter = true)
		{
			Vector3 startPos = obj.transform.position;
			float t = 0f;
			while (t < 1f)
			{
				obj.transform.position = Vector3.Lerp(startPos, finalPosition, t);
				float y = Mathf.Max(obj.transform.position.y + obj.transform.localScale.y / 2f, 0f);
				if (moveCharacter)
				{
					base.transform.position = new Vector3(0f, y, base.transform.position.z);
				}
				t += Time.deltaTime / duration;
				yield return null;
			}
			obj.transform.position = finalPosition;
			if (moveCharacter)
			{
				base.transform.position = new Vector3(0f, obj.transform.localScale.y / 2f, base.transform.position.z);
			}
			yield return 0;
		}

		private IEnumerator PlayClip(int index, bool playIdleOnComplete = false, bool crossfade = false)
		{
			base.transform.rotation.SetLookRotation(Vector3.forward);
			if (clipInfos[index].entryClip != null)
			{
				Debug.LogFormat("PLAYING2: {0} dur {1}", clipInfos[index].entryClip.name, clipInfos[index].entryClip.length);
				if (crossfade)
				{
					animator.CrossFadeInFixedTime(clipInfos[index].entryClip.name, 0.25f);
					yield return new WaitForSeconds(0.25f);
				}
				else
				{
					animator.Play(clipInfos[index].entryClip.name);
				}
			}
			if (clipInfos[index].HasPlatform())
			{
				Vector3 platformBackEdge = clipInfos[index].platformBackEdge;
				Vector3 platformFrontEdge = clipInfos[index].platformFrontEdge;
				float num = Mathf.Abs(platformBackEdge.z - platformFrontEdge.z);
				float z = base.transform.position.z + platformFrontEdge.z + num / 2f;
				float y = clipInfos[index].platformFrontEdge.y;
				platformObject.transform.position = new Vector3(0f, 0f - y, z);
				platformObject.SetActive(value: true);
				platformObject.transform.localScale = new Vector3(platformObject.transform.localScale.x, y * 2f, num);
				if (rightwallObject != null && clipInfos[index].rightWall)
				{
					rightwallObject.SetActive(value: true);
				}
				else
				{
					rightwallObject.SetActive(value: false);
				}
				StartCoroutine(MakeObjectAndCharacterAppear(platformObject, new Vector3(0f, 0f, z), y, 0.5f));
			}
			else
			{
				platformObject.SetActive(value: false);
				base.transform.position = new Vector3(0f, 0f, base.transform.position.z);
			}
			if (clipInfos[index].HasObstacle())
			{
				Vector3 obstacleBackEdge = clipInfos[index].obstacleBackEdge;
				Vector3 obstacleFrontEdge = clipInfos[index].obstacleFrontEdge;
				float num2 = Mathf.Abs(obstacleBackEdge.z - obstacleFrontEdge.z);
				float y2 = clipInfos[index].obstacleFrontEdge.y;
				obstacleObject.transform.localScale = new Vector3(obstacleObject.transform.localScale.x, y2 * 2f, num2);
				obstacleObject.transform.position = new Vector3(0f, (0f - y2) * 1.1f, base.transform.position.z + obstacleFrontEdge.z + num2 / 2f);
				StartCoroutine(MakeObjectAndCharacterAppear(obstacleObject, new Vector3(0f, 0f, base.transform.position.z + obstacleFrontEdge.z + num2 / 2f), 0f, Mathf.Min(0.5f, Mathf.Max(0.25f, (clipInfos[index].entryClip != null) ? clipInfos[index].entryClip.length : 0.25f)), moveCharacter: false));
			}
			if (clipInfos[index].entryClip != null)
			{
				yield return new WaitForSeconds(clipInfos[index].entryClip.length);
			}
			textObject.SetText(clipInfos[index].animationName);
			Debug.LogFormat("PLAYING2: {0} dur {1}", clipInfos[index].GetClipName(), clipInfos[index].clip.length);
			animator.Play(clipInfos[index].GetClipName());
			if (clipInfos[index].animationDurationOverride > 0f)
			{
				yield return new WaitForSeconds(clipInfos[index].animationDurationOverride);
			}
			else if (clipInfos[index].doLeftHandIK || clipInfos[index].doRightHandIK)
			{
				goalZ = base.transform.position.z;
				goalZ += ((clipInfos[index].playerZForMaxIKWeight != -1f) ? clipInfos[index].playerZForMaxIKWeight : clipInfos[index].obstacleFrontEdge.z);
				if (clipInfos[index].IKTarget == AnimationClipDemoInfo.IKTargetLocation.Front)
				{
					handGoalPos = obstacleObject.transform.position + Vector3.up * obstacleObject.transform.localScale.y / 2f - Vector3.forward * obstacleObject.transform.localScale.z / 2f;
				}
				else
				{
					handGoalPos = obstacleObject.transform.position + Vector3.up * obstacleObject.transform.localScale.y / 2f + Vector3.forward * obstacleObject.transform.localScale.z / 2f;
				}
				currentClip = clipInfos[index];
				doingIK = true;
				yield return new WaitForSeconds(clipInfos[index].clip.length);
				doingIK = false;
			}
			else
			{
				yield return new WaitForSeconds(clipInfos[index].clip.length);
			}
			for (int ii = 0; ii < clipInfos[index].preExitClips.Length; ii++)
			{
				animator.Play(clipInfos[index].preExitClips[ii].name);
				yield return new WaitForSeconds(clipInfos[index].preExitClips[ii].length);
			}
			if (clipInfos[index].exitClip != null)
			{
				if (clipInfos[index].HasPlatform())
				{
					StartCoroutine(MakeObjectDisappear(platformObject, clipInfos[index].exitClip.length - 0.1f, sendPlayerBackToGround: true));
				}
				if (clipInfos[index].HasObstacle())
				{
					if (clipInfos[index].disappearObstacleImmediatelyAfterClip)
					{
						StartCoroutine(MakeObjectDisappear(obstacleObject, 0.05f, !clipInfos[index].HasPlatform()));
					}
					else
					{
						float length = clipInfos[index].exitClip.length;
						float delayStart = ((!(length <= 0.35f)) ? (length - 0.35f) : 0f);
						StartCoroutine(MakeObjectDisappear(obstacleObject, 0.35f, !clipInfos[index].HasPlatform(), delayStart));
					}
				}
				Debug.LogFormat("PLAYING2: {0} dur {1}", clipInfos[index].exitClip.name, clipInfos[index].exitClip.length);
				animator.Play(clipInfos[index].exitClip.name);
				yield return new WaitForSeconds(clipInfos[index].exitClip.length);
			}
			base.transform.position = new Vector3(0f, 0f, base.transform.position.z);
			base.transform.rotation = Quaternion.LookRotation(base.transform.forward);
			if (playIdleOnComplete)
			{
				if (crossfade)
				{
					Debug.Log("crossfade");
					animator.CrossFadeInFixedTime(stateToPlayOnStop, 0.25f);
				}
				else
				{
					Debug.Log("Play, not crossfade");
					animator.Play(stateToPlayOnStop);
				}
			}
			yield return 0;
		}

		private void OnAnimatorIK(int layerIndex)
		{
			if (!doingIK)
			{
				animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
				animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
				return;
			}
			float num = Mathf.Abs(goalZ - base.transform.position.z);
			Debug.LogFormat("Doing IK goalZ( {0} ), dist( {1} )", goalZ, num);
			if (num < 1f)
			{
				Debug.LogFormat("Setting IK: {0}", 1f - num);
				float value = Mathf.Clamp01(1f - num);
				if (num < currentClip.fullIKThreshDist)
				{
					value = 1f;
				}
				if (currentClip.doLeftHandIK)
				{
					animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, value);
					animator.SetIKPosition(AvatarIKGoal.LeftHand, handGoalPos + currentClip.leftHandIKOffset);
				}
				if (currentClip.doRightHandIK)
				{
					animator.SetIKPositionWeight(AvatarIKGoal.RightHand, value);
					animator.SetIKPosition(AvatarIKGoal.RightHand, handGoalPos + currentClip.rightHandIKOffset);
				}
			}
			else
			{
				animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
			}
		}
	}
}
