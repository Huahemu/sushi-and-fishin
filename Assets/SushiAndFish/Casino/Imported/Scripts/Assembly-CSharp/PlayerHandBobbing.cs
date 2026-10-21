namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;

public class PlayerHandBobbing : NetworkBehaviour
{
	[SerializeField]
	private PlayerController playerController;

	[SerializeField]
	private float bobbingXMultiplier;

	[SerializeField]
	private float bobbingYMultiplier;

	private CameraSettings _cs;

	private float _xScroll;

	private float _yScroll;

	private Vector3 _finalOffset;

	private bool _hasReset;

	private Vector3 _finalHeadBob;

	private void Awake()
	{
		_cs = SushiAndFish.Casino.Original.CasinoResources.Load<CameraSettings>("CameraSettings");
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (base.isLocalPlayer)
		{
			base.enabled = false;
		}
	}

	private void LateUpdate()
	{
		HeadBob();
	}

	private void HeadBob()
	{
		if (!playerController.hasBody)
		{
			_finalHeadBob = Vector3.zero;
			base.transform.localPosition = Vector3.zero;
			return;
		}
		Vector3 vector = Vector3.ProjectOnPlane(playerController.serverVelocity, Vector3.up);
		bool flag = vector.magnitude > 0.1f;
		if (playerController.isGrounded & flag)
		{
			_hasReset = false;
			_xScroll += Time.deltaTime * _cs.xFrequency * vector.magnitude;
			_yScroll += Time.deltaTime * _cs.yFrequency * vector.magnitude;
			float num = _cs.xCurve.Evaluate(_xScroll);
			float num2 = _cs.yCurve.Evaluate(_yScroll);
			_finalOffset.x = num * _cs.xAmplitude * bobbingXMultiplier * vector.magnitude / 1000f;
			_finalOffset.y = num2 * _cs.yAmplitude * bobbingYMultiplier * vector.magnitude / 1000f;
			_finalHeadBob = Vector3.Lerp(_finalHeadBob, _finalOffset, Time.deltaTime * _cs.headBobLerpSpeed);
		}
		else
		{
			if (!_hasReset)
			{
				_hasReset = true;
				_xScroll = 0f;
				_yScroll = 0f;
				_finalOffset = Vector3.zero;
			}
			_finalHeadBob = Vector3.Lerp(_finalHeadBob, Vector3.zero, Time.deltaTime * _cs.headBobResetLerpSpeed);
		}
		base.transform.localPosition = _finalHeadBob;
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
