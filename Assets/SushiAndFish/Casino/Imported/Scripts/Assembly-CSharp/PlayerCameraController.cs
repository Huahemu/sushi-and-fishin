namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class PlayerCameraController : NetworkBehaviour
{
	private Camera _cam;

	private CameraSettings _cs;

	[SerializeField]
	private Rigidbody rigidBody;

	[SerializeField]
	private PlayerController playerController;

	[SerializeField]
	private PlayerHead playerHead;

	private float _baseFOV;

	private float _runFOVMultiplier = 1f;

	private Vector3 _previousHeadPosition;

	private float _currentTiltX;

	private float _currentTiltZ;

	private float _tiltXVel;

	private float _tiltZVel;

	private Quaternion _finalHeadSway;

	private float _xScroll;

	private float _yScroll;

	private Vector3 _finalOffset;

	private bool _hasReset;

	private Vector3 _finalHeadBob;

	private void Awake()
	{
		_cs = SushiAndFish.Casino.Original.CasinoResources.Load<CameraSettings>("CameraSettings");
		_cam = MonoSingleton<LocalManager>.Instance.mainCamera;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
        if (CasinoRuntimeContext.IsHostIntegration) return;
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
			return;
		}
		_cam.transform.SetParent(base.transform);
		_cam.transform.localPosition = Vector3.zero;
		_cam.transform.localRotation = Quaternion.identity;
	}

	private void Start()
	{
        if (CasinoRuntimeContext.IsHostIntegration) return;
		_baseFOV = _cs.baseFOV.value;
		_cam.fieldOfView = _baseFOV * _runFOVMultiplier;
		_previousHeadPosition = playerHead.transform.position;
	}

	private void LateUpdate()
	{
        if (CasinoRuntimeContext.IsHostIntegration) return;
		SetBaseFOV();
		SetRunFOVMultiplier();
		HeadSway();
		HeadBob();
		SetFinalCameraProperties();
	}

	private void SetFinalCameraProperties()
	{
		_cam.fieldOfView = _baseFOV * _runFOVMultiplier;
		base.transform.localPosition = _finalHeadBob;
		base.transform.localRotation = _finalHeadSway;
	}

	private void SetBaseFOV()
	{
		float b = (InputEvents.IsZoomPressed ? _cs.zoomFOV : _cs.baseFOV.value);
		_baseFOV = Mathf.Lerp(_baseFOV, b, _cs.zoomLerpSpeed * Time.deltaTime);
	}

	private void SetRunFOVMultiplier()
	{
		float b = 1f;
		if (InputEvents.IsSprintPressed && Vector3.ProjectOnPlane(rigidBody.linearVelocity, Vector3.up).magnitude > _cs.runFOVThreshold)
		{
			b = _cs.runFOVMultiplier;
		}
		_runFOVMultiplier = Mathf.Lerp(_runFOVMultiplier, b, _cs.runLerpSpeed * Time.deltaTime);
	}

	private void HeadSway()
	{
		Vector3 direction = (playerHead.transform.position - _previousHeadPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
		_previousHeadPosition = playerHead.transform.position;
		Vector3 vector = playerHead.transform.InverseTransformDirection(direction);
		float target = vector.z * _cs.swayAmountXAxis;
		float target2 = (0f - vector.x) * _cs.swayAmountZAxis;
		_currentTiltX = Mathf.SmoothDampAngle(_currentTiltX, target, ref _tiltXVel, _cs.swayDamping);
		_currentTiltZ = Mathf.SmoothDampAngle(_currentTiltZ, target2, ref _tiltZVel, _cs.swayDamping);
		_finalHeadSway = Quaternion.Euler(_currentTiltX, 0f, _currentTiltZ);
	}

	private void HeadBob()
	{
		if (!_cs.bobbingEnabled)
		{
			_finalHeadBob = Vector3.zero;
			return;
		}
		Vector3 vector = Vector3.ProjectOnPlane(rigidBody.linearVelocity, Vector3.up);
		bool flag = vector.magnitude > 0.1f;
		if (playerController.isGrounded & flag)
		{
			_hasReset = false;
			_xScroll += Time.deltaTime * _cs.xFrequency * vector.magnitude;
			_yScroll += Time.deltaTime * _cs.yFrequency * vector.magnitude;
			float num = _cs.xCurve.Evaluate(_xScroll);
			float num2 = _cs.yCurve.Evaluate(_yScroll);
			_finalOffset.x = num * _cs.xAmplitude * vector.magnitude / 1000f;
			_finalOffset.y = num2 * _cs.yAmplitude * vector.magnitude / 1000f;
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
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
