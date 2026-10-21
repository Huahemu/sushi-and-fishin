using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

public class SphereShellDemo : MonoBehaviour
{
	[SerializeField]
	private Transform m_Center;

	[SerializeField]
	private GameObject m_RedAgentPrefab;

	[SerializeField]
	private GameObject m_BlueAgentPrefab;

	[SerializeField]
	private GameObject m_GreenAgentPrefab;

	private Vector3[] m_CircleSmallPoints;

	private Vector3[] m_CircleMediumPoints;

	private Vector3[] m_CircleLargePoints;

	private Vector3[] m_CircleLarge1Points;

	private void Init()
	{
		GameObject gameObject = new GameObject("AgentsRoot");
		m_CircleSmallPoints = UtilsMath.GetSpaceCirclePoints(m_Center.position, 1f, new Vector3(1f, 1f, 1f), 32);
		m_CircleMediumPoints = UtilsMath.GetSpaceCirclePoints(m_Center.position, 2f, new Vector3(-1f, 1f, 1f), 32);
		m_CircleLargePoints = UtilsMath.GetSpaceCirclePoints(m_Center.position, 3f, new Vector3(1f, 1f, -1f), 64);
		m_CircleLarge1Points = UtilsMath.GetSpaceCirclePoints(m_Center.position + new Vector3(1f, 1f, -1f) * 0.25f, 3f, new Vector3(1f, 1f, -1f), 64);
		Vector3[] circleSmallPoints = m_CircleSmallPoints;
		foreach (Vector3 position in circleSmallPoints)
		{
			Object.Instantiate(m_RedAgentPrefab, position, Quaternion.identity, gameObject.transform).GetComponent<Nav3DAgent>().MoveToPoints(m_CircleSmallPoints, _Loop: true, _StartFromClosest: true);
		}
		circleSmallPoints = m_CircleMediumPoints;
		foreach (Vector3 position2 in circleSmallPoints)
		{
			Object.Instantiate(m_BlueAgentPrefab, position2, Quaternion.identity, gameObject.transform).GetComponent<Nav3DAgent>().MoveToPoints(m_CircleMediumPoints, _Loop: true, _StartFromClosest: true);
		}
		circleSmallPoints = m_CircleLargePoints;
		foreach (Vector3 position3 in circleSmallPoints)
		{
			Object.Instantiate(m_GreenAgentPrefab, position3, Quaternion.identity, gameObject.transform).GetComponent<Nav3DAgent>().MoveToPoints(m_CircleLargePoints, _Loop: true, _StartFromClosest: true);
		}
		circleSmallPoints = m_CircleLarge1Points;
		foreach (Vector3 position4 in circleSmallPoints)
		{
			Object.Instantiate(m_GreenAgentPrefab, position4, Quaternion.identity, gameObject.transform).GetComponent<Nav3DAgent>().MoveToPoints(m_CircleLarge1Points, _Loop: true, _StartFromClosest: true);
		}
	}

	private void DrawPointsSequence(Vector3[] _Points, Color _Color)
	{
		if (_Points != null)
		{
			Gizmos.color = _Color;
			for (int i = 0; i < _Points.Length - 1; i++)
			{
				Gizmos.DrawLine(_Points[i], _Points[i + 1]);
			}
		}
	}

	private void Start()
	{
		Nav3DManager.OnNav3DInit += Init;
	}

	private void OnDrawGizmos()
	{
		if (base.enabled && Application.isPlaying)
		{
			DrawPointsSequence(m_CircleSmallPoints, Color.red);
			DrawPointsSequence(m_CircleMediumPoints, Color.blue);
			DrawPointsSequence(m_CircleLargePoints, Color.green);
		}
	}
}
