using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Demo
{
	public class SquareWanderingAgent : MonoBehaviour
	{
		private const float X_MIN = 0f;

		private const float X_MAX = 500f;

		private const float Y_MIN = 0f;

		private const float Y_MAX = 100f;

		private const float Z_MIN = 0f;

		private const float Z_MAX = 500f;

		private const float DEFAULT_TIME_RANGE_MIN = 5f;

		private const float DEFAULT_TIME_RANGE_MAX = 15f;

		private Nav3DAgent m_Agent;

		private Log m_Log;

		[SerializeField]
		private float m_XMin;

		[SerializeField]
		private float m_YMin;

		[SerializeField]
		private float m_ZMin;

		[SerializeField]
		private float m_XMax = 500f;

		[SerializeField]
		private float m_YMax = 100f;

		[SerializeField]
		private float m_ZMax = 500f;

		[SerializeField]
		private float m_TimeRangeMin = 5f;

		[SerializeField]
		private float m_TimeRangeMax = 15f;

		private void Awake()
		{
			m_Agent = GetComponent<Nav3DAgent>();
			m_Log = new Log(base.name, 100);
			MoveToArbitraryPoint();
		}

		public void Init(float _XMin, float _YMin, float _ZMin, float _XMax, float _YMax, float _ZMax, float _TimeRange)
		{
			m_XMin = _XMin;
			m_YMin = _YMin;
			m_ZMin = _ZMin;
			m_XMax = _XMax;
			m_YMax = _YMax;
			m_ZMax = _ZMax;
			m_TimeRangeMax = _TimeRange;
		}

		public void StartMovement()
		{
			MoveToArbitraryPoint();
		}

		private void MoveToArbitraryPoint()
		{
			m_Log.Write("MoveToArbitraryPoint invoked");
			Vector3 point = (GetRandomBool() ? new Vector3(GetRandomBool() ? m_XMin : m_XMax, Random.Range(m_YMin, m_YMax), Random.Range(m_ZMin, m_ZMax)) : new Vector3(Random.Range(m_XMin, m_XMax), Random.Range(m_YMin, m_YMax), GetRandomBool() ? m_ZMin : m_ZMax));
			m_Agent.MoveTo(point, () =>
			{
				m_Log.Write("MoveTo: OnReach invoked");
				MoveToArbitraryPoint();
			}, MoveToPointAfterDelay, (PathfindingResult _Result) =>
			{
				Debug.LogError(_Result.ErrorMessage);
				MoveToArbitraryPoint();
			});
			void MoveToPointAfterDelay()
			{
				StartCoroutine(Coroutines.DoOnDelay(() =>
				{
					m_Log.Write("DoOnDelay invoked");
					MoveToArbitraryPoint();
				}, Random.Range(m_TimeRangeMin, m_TimeRangeMax)));
			}
		}

		private bool GetRandomBool()
		{
			return Random.Range(0, 2) == 0;
		}
	}
}
