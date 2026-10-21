namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Linq;
using global::DG.Tweening;
using global::FMOD.Studio;
using global::FMODUnity;
using global::UnityEngine;

public class WheelLocal : MonoBehaviour
{
	[Header("Wheel Settings")]
	[SerializeField]
	private float spinDuration = 3f;

	[SerializeField]
	private int minTurnAmount = 3;

	[SerializeField]
	private bool spinDirection;

	[SerializeField]
	private Ease easing = Ease.OutCubic;

	[Header("References")]
	[SerializeField]
	private Transform wheelTransform;

	[SerializeField]
	private Transform resultsParent;

	private WheelResult[] _results;

	[Header("SFX")]
	[SerializeField]
	private EventReference sfxSpinEvent;

	private EventInstance sfxSpinInstance;

	private bool _isSpinning;

	private WheelResult[] Results
	{
		get
		{
			if (_results == null || _results.Length == 0)
			{
				_results = resultsParent.GetComponentsInChildren<WheelResult>();
			}
			return _results;
		}
	}

	public void SpinTheWheel()
	{
		if (!_isSpinning)
		{
			_isSpinning = true;
			float num = Random.Range(0f, 360f);
			float num2 = (float)minTurnAmount * 360f + num;
			if (spinDirection)
			{
				num2 *= -1f;
			}
			SpinWheel(num2, spinDuration);
			StartCoroutine(WaitAndStop());
		}
	}

	private IEnumerator WaitAndStop()
	{
		yield return new WaitForSeconds(spinDuration);
		_isSpinning = false;
		FindResult();
		sfxSpinInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
		sfxSpinInstance.release();
	}

	private void FindResult()
	{
		int index = (from x in Results.Select((WheelResult r, int i) => new
			{
				Result = r,
				Index = i
			})
			orderby x.Result.transform.position.y descending
			select x).First().Index;
		ResultFeedback(index);
	}

	private void SpinWheel(float finalAngle, float duration)
	{
		wheelTransform.DOLocalRotate(new Vector3(0f, 0f, 0f - finalAngle), duration, RotateMode.FastBeyond360).SetEase(easing);
		sfxSpinInstance = RuntimeManager.CreateInstance(sfxSpinEvent);
		sfxSpinInstance.set3DAttributes(base.transform.position.To3DAttributes());
		sfxSpinInstance.setParameterByName("spinDuration", duration * 1000f);
		sfxSpinInstance.start();
	}

	private void ResultFeedback(int index)
	{
		Results[index].SelectedResultFeedback();
	}
}

}
