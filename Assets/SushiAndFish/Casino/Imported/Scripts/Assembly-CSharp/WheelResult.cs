namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::UnityEngine;

public class WheelResult : MonoBehaviour
{
	public string result;

	[SerializeField]
	private Transform meshTransform;

	public void SelectedResultFeedback()
	{
		meshTransform.DOPunchScale(meshTransform.localScale * 1.2f, 0.5f, 1);
	}
}

}
