using System;
using System.Collections;
using Nav3D.API;
using UnityEngine;

namespace Nav3D.Demo
{
	[RequireComponent(typeof(Nav3DAgent))]
	public class MinerController : MonoBehaviour
	{
		private Nav3DAgent m_Agent;

		private ContactPointsController m_Mine;

		private ContactPointsController m_Planet;

		private Vector3 m_MineContactPoint;

		private Vector3 m_PlanetContactPoint;

		public void Init(ContactPointsController _Planet, ContactPointsController _Mine)
		{
			m_Planet = _Planet;
			m_Mine = _Mine;
			TeleportToPlanetAndGo(_ForceTpToPlanet: true);
		}

		private void TeleportToPlanetAndGo(bool _ForceTpToPlanet = false)
		{
			if (_ForceTpToPlanet)
			{
				base.transform.position = m_Planet.GetRandomFreeTouchPoint() ?? m_Planet.transform.position;
			}
			SelectMinePointAndGo();
		}

		private void SelectMinePointAndGo()
		{
			Vector3? closestFreeTouchPoint = m_Mine.GetClosestFreeTouchPoint(base.transform.position);
			if (closestFreeTouchPoint.HasValue)
			{
				m_Planet.ReleaseTouchPoint(m_PlanetContactPoint);
				m_MineContactPoint = closestFreeTouchPoint.Value;
				m_Mine.OccupyTouchPoint(m_MineContactPoint);
				m_Agent.MoveTo(m_MineContactPoint, OnMineReached, null, ErrorHandler);
			}
			else
			{
				StartCoroutine(DoAfterDelay(SelectMinePointAndGo, 3f));
			}
		}

		private IEnumerator DoAfterDelay(Action _Do, float _Delay)
		{
			yield return new WaitForSeconds(_Delay);
			_Do?.Invoke();
		}

		private void OnMineReached()
		{
			StartCoroutine(DoAfterDelay(() =>
			{
				Vector3? closestFreeTouchPoint = m_Planet.GetClosestFreeTouchPoint(base.transform.position);
				if (closestFreeTouchPoint.HasValue)
				{
					m_Mine.ReleaseTouchPoint(m_MineContactPoint);
					m_PlanetContactPoint = closestFreeTouchPoint.Value;
					m_Planet.OccupyTouchPoint(m_PlanetContactPoint);
					m_Agent.MoveTo(m_PlanetContactPoint, SelectMinePointAndGo, null, ErrorHandler);
				}
				else
				{
					StartCoroutine(DoAfterDelay(OnMineReached, 3f));
				}
			}, 1f));
		}

		private void ErrorHandler(PathfindingResult _Result)
		{
			Debug.LogWarning(base.name + ": " + _Result.ErrorMessage);
			m_Mine.ReleaseTouchPoint(m_MineContactPoint);
			m_Planet.ReleaseTouchPoint(m_PlanetContactPoint);
			TeleportToPlanetAndGo(_ForceTpToPlanet: true);
		}

		private void Awake()
		{
			m_Agent = GetComponent<Nav3DAgent>();
		}
	}
}
