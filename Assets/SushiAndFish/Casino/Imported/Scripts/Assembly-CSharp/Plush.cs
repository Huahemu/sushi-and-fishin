namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::UnityEngine;

public class Plush : Item
{
	[SerializeField]
	private SFXComponent squeezeSfx;

	[SerializeField]
	private PolleSFX polleSfx;

	private Tween _scaleTween;

	protected override void OnUseItem(bool isPressed)
	{
		if (isPressed)
		{
			Squeeze();
			if ((bool)base.NetworkHolder && base.NetworkHolder.isLocalPlayer && (bool)polleSfx)
			{
				polleSfx.PlayPolleSays();
			}
		}
	}

	private void Squeeze()
	{
		_scaleTween?.Kill();
		Sequence sequence = DOTween.Sequence();
		sequence.Append(modelTransform.DOScale(new Vector3(1.1f, 0.9f, 0.9f), 0.05f).SetEase(Ease.OutQuad));
		sequence.Append(modelTransform.DOScale(new Vector3(0.7f, 1.1f, 1.1f), 0.1f).SetEase(Ease.OutQuad));
		sequence.Append(modelTransform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutElastic, 1.5f, 0.3f));
		_scaleTween = sequence;
		if ((bool)squeezeSfx)
		{
			squeezeSfx.PlayOneShotAttached();
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
