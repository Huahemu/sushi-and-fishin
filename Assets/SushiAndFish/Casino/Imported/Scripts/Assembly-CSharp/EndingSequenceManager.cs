namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::Unity.Cinemachine;
using global::UnityEngine;
using global::UnityEngine.Events;
using global::UnityEngine.UI;

public class EndingSequenceManager : NetworkSingleton<EndingSequenceManager>
{
	[Serializable]
	public class CreditStep
	{
		[Range(0f, 1f)]
		public float threshold;

		public int fromCanvasIndex;

		public int toCanvasIndex;

		public float fadeDuration = 2f;

		public string debugLabel;
	}

	[Header("Settings")]
	[SerializeField]
	private float initialDelay = 2f;

	[SerializeField]
	private float barFadeDuration = 1f;

	[SerializeField]
	private float splineDuration = 15f;

	[SerializeField]
	private float cameraOffsetDuration = 2f;

	[SerializeField]
	private float creditsDuration = 20f;

	[SerializeField]
	private float skipTime = 2f;

	[SerializeField]
	private float backgroundFadeDuration = 1f;

	[SerializeField]
	private float delayBeforeSkip = 2f;

	[Header("References")]
	[SerializeField]
	private SkipUI skipUI;

	[SerializeField]
	private CinemachineSplineDolly splineDolly;

	[SerializeField]
	private CinemachineCameraOffset cameraOffset;

	[SerializeField]
	private SteamAchievement_SteamworksNET achievementToUnlock;

	[SerializeField]
	private Image[] bars;

	[SerializeField]
	private Image background;

	[SerializeField]
	private CanvasGroup[] creditSteps;

	[SerializeField]
	private CreditStep[] sequence;

	private bool _isBeenFading;

	private int currentStepIndex;

	[Header("CutsceneSpecific")]
	[SerializeField]
	private Transform[] thingsToLookAtInOrder;

	private void Start()
	{
		InitBaseValues();
		achievementToUnlock.UnlockAchievement();
		MonoSingleton<LocalManager>.Instance.mainCamera.cullingMask |= 1 << SushiAndFish.Casino.CasinoLayers.NameToLayer("SelfMeshPlayer");
		skipUI.Reset();
	}

	private void InitBaseValues()
	{
		Image[] array = bars;
		foreach (Image obj in array)
		{
			Color color = obj.color;
			color.a = 0f;
			obj.color = color;
		}
		CanvasGroup[] array2 = creditSteps;
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].alpha = 0f;
		}
	}

	private void CrossFadeBetweenTwoCanvasGroups(CanvasGroup from, CanvasGroup to, float duration)
	{
		if (!_isBeenFading)
		{
			_isBeenFading = true;
			from.DOFade(0f, duration).SetEase(Ease.InOutSine);
			to.DOFade(1f, duration).SetEase(Ease.InOutSine).OnComplete(() =>
			{
				_isBeenFading = false;
			});
		}
	}

	private void OnEnable()
	{
		SkipUI obj = skipUI;
		obj.OnSkipped = (UnityAction)Delegate.Combine(obj.OnSkipped, new UnityAction(OnSkip));
	}

	private void OnDisable()
	{
		SkipUI obj = skipUI;
		obj.OnSkipped = (UnityAction)Delegate.Remove(obj.OnSkipped, new UnityAction(OnSkip));
	}

	private void OnSkip()
	{
	}

	[Server]
	public void ServerStartSequence()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void EndingSequenceManager::ServerStartSequence()' called when server was not active");
		}
		else
		{
			RpcStartSequence();
		}
	}

	[ClientRpc]
	private void RpcStartSequence()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void EndingSequenceManager::RpcStartSequence()", -2144402586, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void TestLocal()
	{
		StartCoroutine(SequenceRoutine(thingsToLookAtInOrder));
	}

	private void Update()
	{
		float cameraPosition = splineDolly.CameraPosition;
		while (currentStepIndex < sequence.Length && cameraPosition >= sequence[currentStepIndex].threshold)
		{
			ExecuteStep(sequence[currentStepIndex], currentStepIndex);
			currentStepIndex++;
		}
	}

	private void ExecuteStep(CreditStep step, int stepIndex)
	{
		if (creditSteps == null || creditSteps.Length == 0)
		{
			Debug.LogWarning("Credit steps array is empty.");
			return;
		}
		if (step.fromCanvasIndex < 0 || step.fromCanvasIndex >= creditSteps.Length || step.toCanvasIndex < 0 || step.toCanvasIndex >= creditSteps.Length)
		{
			Debug.LogWarning($"Invalid canvas index at sequence step {stepIndex}.");
			return;
		}
		Debug.Log(string.IsNullOrWhiteSpace(step.debugLabel) ? $"step {stepIndex + 1}" : step.debugLabel);
		CrossFadeBetweenTwoCanvasGroups(creditSteps[step.fromCanvasIndex], creditSteps[step.toCanvasIndex], step.fadeDuration);
	}

	public void ResetSequence()
	{
		currentStepIndex = 0;
	}

	private IEnumerator SequenceRoutine(Transform[] lookAtTargets)
	{
		skipUI.SetSkippableServer();
		splineDolly.CameraPosition = 0f;
		float camPositionAlongSpline = splineDolly.CameraPosition;
		DOTween.To(() => camPositionAlongSpline, (float x) =>
		{
			splineDolly.CameraPosition = x;
		}, 1f, splineDuration).SetEase(Ease.Linear);
		yield return new WaitForSeconds(initialDelay);
		skipUI.SetSkippableForLocal();
		Image[] array = bars;
		for (int num = 0; num < array.Length; num++)
		{
			array[num].DOColor(Color.black, barFadeDuration);
		}
		yield return new WaitForSeconds(barFadeDuration);
		if ((bool)thingsToLookAtInOrder[0])
		{
			splineDolly.VirtualCamera.Follow = thingsToLookAtInOrder[0];
		}
		if (creditSteps.Length != 0 && (bool)creditSteps[0])
		{
			creditSteps[0].DOFade(1f, barFadeDuration);
		}
		yield return new WaitForSeconds(splineDuration + 0.5f);
		background.DOColor(new Color(0f, 0f, 0f, 0.5f), backgroundFadeDuration);
		yield return new WaitForSeconds(backgroundFadeDuration);
		yield return new WaitForSeconds(delayBeforeSkip);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcStartSequence()
	{
		StartCoroutine(SequenceRoutine(thingsToLookAtInOrder));
	}

	protected static void InvokeUserCode_RpcStartSequence(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartSequence called on server.");
		}
		else
		{
			((EndingSequenceManager)obj).UserCode_RpcStartSequence();
		}
	}

	static EndingSequenceManager()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(EndingSequenceManager), "System.Void EndingSequenceManager::RpcStartSequence()", InvokeUserCode_RpcStartSequence);
	}
}

}
