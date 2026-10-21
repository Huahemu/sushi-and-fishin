using UnityEngine;

namespace PaperPixelGames.Debugging
{
	[DisallowMultipleComponent]
	public sealed class TransformJitterDebugView : MonoBehaviour
	{
		[Header("Targets")]
		[Tooltip("Usually the physics-driven root (ECM2 character transform). If null, uses this.transform.")]
		[SerializeField]
		private Transform _target;

		[Tooltip("Optional: camera or camera parent you visually care about.")]
		[SerializeField]
		private Transform _cameraTarget;

		[Header("UI")]
		[SerializeField]
		private bool _showOverlay = true;

		[SerializeField]
		private bool _showSceneLines = true;

		[SerializeField]
		private int _fontSize = 14;

		[Header("Thresholds")]
		[Tooltip("If Update->Late delta exceeds this, likely multiple writers this frame.")]
		[SerializeField]
		private float _suspiciousUpdateLateDelta = 0.0015f;

		[Tooltip("If Fixed->Render delta exceeds this consistently, interpolation mismatch is likely.")]
		[SerializeField]
		private float _suspiciousFixedRenderDelta = 0.01f;

		private Vector3 _fixedPosPrev;

		private Vector3 _fixedPosCurr;

		private Quaternion _fixedRotPrev;

		private Quaternion _fixedRotCurr;

		private int _fixedTick;

		private Vector3 _updatePos;

		private Quaternion _updateRot;

		private Vector3 _latePos;

		private Quaternion _lateRot;

		private int _frame;

		private int _transformWritesThisFrame;

		private Vector3 _lastObservedPos;

		private Quaternion _lastObservedRot;

		private float _fixedToRenderPosDelta;

		private float _updateToLatePosDelta;

		private float _fixedToRenderYawDeltaDeg;

		private float _updateToLateYawDeltaDeg;

		private float _camFixedToRenderDelta;

		private float _camUpdateToLateDelta;

		private static float YawDeg(Quaternion q)
		{
			float num = q.eulerAngles.y;
			if (num > 180f)
			{
				num -= 360f;
			}
			return num;
		}

		private static float DeltaAngleDeg(float a, float b)
		{
			return Mathf.Abs(Mathf.DeltaAngle(a, b));
		}

		private void Awake()
		{
			if (_target == null)
			{
				_target = base.transform;
			}
			_fixedPosPrev = _target.position;
			_fixedPosCurr = _target.position;
			_fixedRotPrev = _target.rotation;
			_fixedRotCurr = _target.rotation;
			_lastObservedPos = _target.position;
			_lastObservedRot = _target.rotation;
		}

		private void FixedUpdate()
		{
			_fixedPosPrev = _fixedPosCurr;
			_fixedRotPrev = _fixedRotCurr;
			_fixedPosCurr = _target.position;
			_fixedRotCurr = _target.rotation;
			_fixedTick++;
		}

		private void Update()
		{
			if (Time.frameCount != _frame)
			{
				_frame = Time.frameCount;
				_transformWritesThisFrame = 0;
			}
			_updatePos = _target.position;
			_updateRot = _target.rotation;
			CountWrites(_updatePos, _updateRot);
			ComputeDeltas(usingLate: false);
		}

		private void LateUpdate()
		{
			_latePos = _target.position;
			_lateRot = _target.rotation;
			CountWrites(_latePos, _lateRot);
			ComputeDeltas(usingLate: true);
		}

		private void CountWrites(Vector3 pos, Quaternion rot)
		{
			if ((pos - _lastObservedPos).sqrMagnitude > 1E-07f || Quaternion.Angle(rot, _lastObservedRot) > 0.001f)
			{
				_transformWritesThisFrame++;
			}
			_lastObservedPos = pos;
			_lastObservedRot = rot;
		}

		private void ComputeDeltas(bool usingLate)
		{
			Vector3 b = (usingLate ? _latePos : _updatePos);
			Quaternion q = (usingLate ? _lateRot : _updateRot);
			_fixedToRenderPosDelta = Vector3.Distance(_fixedPosCurr, b);
			float a = YawDeg(_fixedRotCurr);
			float b2 = YawDeg(q);
			_fixedToRenderYawDeltaDeg = DeltaAngleDeg(a, b2);
			_updateToLatePosDelta = Vector3.Distance(_updatePos, _latePos);
			_updateToLateYawDeltaDeg = DeltaAngleDeg(YawDeg(_updateRot), YawDeg(_lateRot));
			if (_cameraTarget != null)
			{
				_camFixedToRenderDelta = Vector3.Distance(_cameraTarget.position, _cameraTarget.position);
				_camUpdateToLateDelta = 0f;
			}
		}

		private void OnGUI()
		{
			if (_showOverlay)
			{
				GUIStyle style = new GUIStyle(GUI.skin.label)
				{
					fontSize = _fontSize,
					richText = true
				};
				bool flag = _updateToLatePosDelta > _suspiciousUpdateLateDelta || _transformWritesThisFrame >= 2;
				bool num = _fixedToRenderPosDelta > _suspiciousFixedRenderDelta;
				string text = (flag ? "<color=yellow>YES</color>" : "<color=green>no</color>");
				string arg = (num ? "<color=yellow>YES</color>" : "<color=green>no</color>");
				string text2 = "<b>[Jitter Debug]</b>\nTarget: " + _target.name + "\n" + $"FixedTick: {_fixedTick}\n\n" + "<b>Fixed → Render</b>\n" + $"Pos Δ: <b>{_fixedToRenderPosDelta:F5}</b>  (suspicious? {arg})\n" + $"Yaw Δ: <b>{_fixedToRenderYawDeltaDeg:F3}°</b>\n\n" + "<b>Update → Late</b>\n" + $"Pos Δ: <b>{_updateToLatePosDelta:F5}</b>\n" + $"Yaw Δ: <b>{_updateToLateYawDeltaDeg:F3}°</b>\n\n" + "<b>Multiple writers this frame?</b> " + text + "\n" + $"Observed writes (approx): <b>{_transformWritesThisFrame}</b>\n" + "Hint: If Update→Late Δ spikes while turning, something else is writing in Late (constraints/cinemachine/network/etc).";
				GUI.Box(new Rect(10f, 10f, 520f, 220f), GUIContent.none);
				GUI.Label(new Rect(20f, 20f, 500f, 200f), text2, style);
			}
		}

		private void OnDrawGizmos()
		{
			if (_showSceneLines && !(_target == null))
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(_fixedPosCurr, (_latePos == Vector3.zero) ? _target.position : _latePos);
				Gizmos.color = Color.cyan;
				Gizmos.DrawSphere(_fixedPosCurr, 0.03f);
				Gizmos.color = Color.magenta;
				Gizmos.DrawSphere((_latePos == Vector3.zero) ? _target.position : _latePos, 0.03f);
			}
		}
	}
}
