namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

[DisallowMultipleComponent]
public class UIButtonFeedback : MonoBehaviour
{
	[SerializeField]
	private float lerpDuration = 0.12f;

	[SerializeField]
	private Vector3 feedbackScale = new Vector3(1.05f, 1.05f, 1f);

	private Image[] _childImages;

	private TMP_Text[] _childTmpTexts;

	private Text[] _childLegacyTexts;

	private Color[] _baseImageColors;

	private Color[] _baseTmpTextColors;

	private Color[] _baseLegacyTextColors;

	private Vector3 _baseScale;

	private Coroutine _activeLerp;

	private void Awake()
	{
		_baseScale = base.transform.localScale;
		CacheChildren();
		CacheBaseValues();
	}

	public void PlayFeedback()
	{
		StartLerp(toFeedback: true);
	}

	public void RevertFeedback()
	{
		StartLerp(toFeedback: false);
	}

	private void OnDisable()
	{
		if (_activeLerp != null)
		{
			StopCoroutine(_activeLerp);
			_activeLerp = null;
		}
		ApplyInstantBaseValues();
	}

	private void CacheChildren()
	{
		List<Image> list = new List<Image>();
		Image[] componentsInChildren = GetComponentsInChildren<Image>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i].transform != base.transform)
			{
				list.Add(componentsInChildren[i]);
			}
		}
		_childImages = list.ToArray();
		List<TMP_Text> list2 = new List<TMP_Text>();
		TMP_Text[] componentsInChildren2 = GetComponentsInChildren<TMP_Text>(includeInactive: true);
		for (int j = 0; j < componentsInChildren2.Length; j++)
		{
			if (componentsInChildren2[j].transform != base.transform)
			{
				list2.Add(componentsInChildren2[j]);
			}
		}
		_childTmpTexts = list2.ToArray();
		List<Text> list3 = new List<Text>();
		Text[] componentsInChildren3 = GetComponentsInChildren<Text>(includeInactive: true);
		for (int k = 0; k < componentsInChildren3.Length; k++)
		{
			if (componentsInChildren3[k].transform != base.transform)
			{
				list3.Add(componentsInChildren3[k]);
			}
		}
		_childLegacyTexts = list3.ToArray();
	}

	private void CacheBaseValues()
	{
		_baseImageColors = new Color[_childImages.Length];
		for (int i = 0; i < _childImages.Length; i++)
		{
			_baseImageColors[i] = _childImages[i].color;
		}
		_baseTmpTextColors = new Color[_childTmpTexts.Length];
		for (int j = 0; j < _childTmpTexts.Length; j++)
		{
			_baseTmpTextColors[j] = _childTmpTexts[j].color;
		}
		_baseLegacyTextColors = new Color[_childLegacyTexts.Length];
		for (int k = 0; k < _childLegacyTexts.Length; k++)
		{
			_baseLegacyTextColors[k] = _childLegacyTexts[k].color;
		}
	}

	private void StartLerp(bool toFeedback)
	{
		if (_activeLerp != null)
		{
			StopCoroutine(_activeLerp);
		}
		_activeLerp = StartCoroutine(LerpRoutine(toFeedback));
	}

	private IEnumerator LerpRoutine(bool toFeedback)
	{
		Vector3 fromScale = base.transform.localScale;
		Vector3 toScale = (toFeedback ? feedbackScale : _baseScale);
		Color[] fromImageColors = new Color[_childImages.Length];
		Color[] toImageColors = new Color[_childImages.Length];
		for (int i = 0; i < _childImages.Length; i++)
		{
			fromImageColors[i] = _childImages[i].color;
			Color color = _baseImageColors[i];
			toImageColors[i] = (toFeedback ? new Color(color.r, color.g, color.b, 1f) : color);
		}
		Color[] fromTmpColors = new Color[_childTmpTexts.Length];
		Color[] toTmpColors = new Color[_childTmpTexts.Length];
		for (int j = 0; j < _childTmpTexts.Length; j++)
		{
			fromTmpColors[j] = _childTmpTexts[j].color;
			Color color2 = _baseTmpTextColors[j];
			toTmpColors[j] = (toFeedback ? new Color(0f, 0f, 0f, color2.a) : color2);
		}
		Color[] fromLegacyColors = new Color[_childLegacyTexts.Length];
		Color[] toLegacyColors = new Color[_childLegacyTexts.Length];
		for (int k = 0; k < _childLegacyTexts.Length; k++)
		{
			fromLegacyColors[k] = _childLegacyTexts[k].color;
			Color color3 = _baseLegacyTextColors[k];
			toLegacyColors[k] = (toFeedback ? new Color(0f, 0f, 0f, color3.a) : color3);
		}
		float elapsed = 0f;
		while (elapsed < lerpDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / lerpDuration);
			base.transform.localScale = Vector3.Lerp(fromScale, toScale, t);
			for (int l = 0; l < _childImages.Length; l++)
			{
				_childImages[l].color = Color.Lerp(fromImageColors[l], toImageColors[l], t);
			}
			for (int m = 0; m < _childTmpTexts.Length; m++)
			{
				_childTmpTexts[m].color = Color.Lerp(fromTmpColors[m], toTmpColors[m], t);
			}
			for (int n = 0; n < _childLegacyTexts.Length; n++)
			{
				_childLegacyTexts[n].color = Color.Lerp(fromLegacyColors[n], toLegacyColors[n], t);
			}
			yield return null;
		}
		base.transform.localScale = toScale;
		for (int num = 0; num < _childImages.Length; num++)
		{
			_childImages[num].color = toImageColors[num];
		}
		for (int num2 = 0; num2 < _childTmpTexts.Length; num2++)
		{
			_childTmpTexts[num2].color = toTmpColors[num2];
		}
		for (int num3 = 0; num3 < _childLegacyTexts.Length; num3++)
		{
			_childLegacyTexts[num3].color = toLegacyColors[num3];
		}
		_activeLerp = null;
	}

	private void ApplyInstantBaseValues()
	{
		base.transform.localScale = _baseScale;
		for (int i = 0; i < _childImages.Length; i++)
		{
			_childImages[i].color = _baseImageColors[i];
		}
		for (int j = 0; j < _childTmpTexts.Length; j++)
		{
			_childTmpTexts[j].color = _baseTmpTextColors[j];
		}
		for (int k = 0; k < _childLegacyTexts.Length; k++)
		{
			_childLegacyTexts[k].color = _baseLegacyTextColors[k];
		}
	}
}

}
