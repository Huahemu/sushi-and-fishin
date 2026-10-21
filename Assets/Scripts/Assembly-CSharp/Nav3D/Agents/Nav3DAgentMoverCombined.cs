using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.API;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.Agents
{
	public class Nav3DAgentMoverCombined : Nav3DAgentMoverGlobal
	{
		private DateTime m_LastPathUpdateFireTime;

		private bool m_DeviatedFromPath;

		private bool m_DeviatedFromPathLastFrame;

		private Vector3 m_VPreOptMovers = Vector3.zero;

		private Vector3 m_VPreOptStatic = Vector3.zero;

		private Vector3 m_LocalAvoidanceVelocity;

		private Vector3 m_BlendedVelocity;

		public override bool Avoiding => !base.CoDirectionalAvoidanceInterrupt;

		private Vector3 VPref => (m_PathNextPoint - m_CurrentPosition).normalized * m_Config.Speed;

		private float VelocityDangerDistance => m_Config.VelocityRadius * 2f;

		public Nav3DAgentMoverCombined(Vector3 _Position, Nav3DAgentConfig _Config, Nav3DAgent _Agent, Log _Log)
			: base(_Position, _Config, _Agent, _Log)
		{
			InitLocalAvoidanceSolvers();
		}

		public override bool IsDeviatedFromPathLastFrame()
		{
			return m_DeviatedFromPathLastFrame;
		}

		public override void UpdateRivalVO(Nav3DAgentMover _RivalMover, VOAgent _RivalVO)
		{
			m_MoversVOsProvider?.UpdateRivalVO(_RivalMover, _RivalVO);
		}

		protected override Vector3 GetVelocity()
		{
			m_DeviatedFromPathLastFrame = false;
			if (m_Path == null || !m_Path.IsValid || m_CurrFollowData == null || !m_CurrFollowData.IsValid)
			{
				return base.LastVelocity = Vector3.zero;
			}
			if (base.CoDirectionalAvoidanceInterrupt)
			{
				SetAvoidanceInterrupt(_Value: false);
				return base.LastVelocity = Vector3.zero;
			}
			if (m_Config.AutoUpdatePath && (AgentManager.Now - m_LastPathUpdateFireTime).TotalMilliseconds > (double)Mathf.Max(m_Config.PathAutoUpdateCooldown, m_Config.PathfindingTimeout))
			{
				m_CurrFollowData.GetDistToClosestOnPath(m_CurrentPosition, out var _, out var _Magnitude);
				if (_Magnitude > VelocityDangerDistance)
				{
					RestartPathFollowing();
					m_LastPathUpdateFireTime = AgentManager.Now;
					m_DeviatedFromPath = true;
					m_DeviatedFromPathLastFrame = true;
					return base.LastVelocity = Vector3.zero;
				}
			}
			m_PathNextPoint = m_CurrFollowData.GetMovePoint(m_Config.Speed, m_Config.Radius, m_CurrentPosition);
			IVO iVO = null;
			if (m_Config.AvoidStaticObstacles)
			{
				iVO = m_TrianglesVOProvider.GetVO();
			}
			List<VOAgent> vOs = m_MoversVOsProvider.GetVOs();
			if (base.CoDirectionalAvoidanceInterrupt)
			{
				return base.LastVelocity = Vector3.zero;
			}
			if (vOs.Any())
			{
				List<Nav3D.LocalAvoidance.SupportingMath.Plane> list = new List<Nav3D.LocalAvoidance.SupportingMath.Plane>(vOs.Count);
				list.AddRange(vOs.Select((VOAgent _VO) => _VO.GetORCA()));
				if (m_VPreOptMovers == Vector3.zero)
				{
					m_VPreOptMovers = VPref;
				}
				Vector3 vector = LPSolver.Instance.SolveMax(list, new Sphere(Vector3.zero, m_Config.MaxSpeed), m_VPreOptMovers);
				if (vector.sqrMagnitude > m_Config.SqrMaxSpeed)
				{
					vector = Vector3.ClampMagnitude(vector, m_Config.MaxSpeed);
				}
				m_VPreOptMovers = vector;
			}
			else
			{
				m_VPreOptMovers = VPref;
			}
			if (iVO != null)
			{
				if (m_VPreOptStatic == Vector3.zero)
				{
					m_VPreOptStatic = VPref;
				}
				Vector3 vector2 = LPSolver.Instance.SolveMax(new List<Nav3D.LocalAvoidance.SupportingMath.Plane> { iVO.GetORCA() }, new Sphere(Vector3.zero, m_Config.MaxSpeed), m_VPreOptStatic);
				if (vector2.sqrMagnitude > m_Config.SqrMaxSpeed)
				{
					vector2 = Vector3.ClampMagnitude(vector2, m_Config.MaxSpeed);
				}
				m_VPreOptStatic = vector2;
			}
			else
			{
				m_VPreOptStatic = VPref;
			}
			Vector3 vPref = VPref;
			Vector3 vector3;
			if (!vOs.Any() && iVO == null)
			{
				m_LocalAvoidanceVelocity = Vector3.zero;
				vector3 = Vector3.Slerp(vPref, base.LastVelocity, 0.1f);
			}
			else if (!vOs.Any())
			{
				m_LocalAvoidanceVelocity = m_VPreOptStatic;
				vector3 = UtilsMath.WeightedVector3Sum2(vPref, m_VPreOptStatic, m_Config.PathVelocityWeight1, m_Config.ObstaclesAvoidanceVelocityWeight1);
			}
			else if (iVO == null)
			{
				m_LocalAvoidanceVelocity = m_VPreOptMovers;
				vector3 = UtilsMath.WeightedVector3Sum2(vPref, m_VPreOptMovers, m_Config.PathVelocityWeight2, m_Config.AgentsAvoidanceVelocityWeight1);
			}
			else
			{
				vector3 = UtilsMath.WeightedVector3Sum3(vPref, m_VPreOptMovers, m_VPreOptStatic, m_Config.PathVelocityWeight, m_Config.AgentsAvoidanceVelocityWeight, m_Config.ObstaclesAvoidanceVelocityWeight, out m_LocalAvoidanceVelocity);
			}
			base.LastVelocity = vector3;
			return vector3;
		}

		protected override void OnTargetPointPassed(Vector3 _Point)
		{
			base.OnTargetPointPassed(_Point);
			if (m_DeviatedFromPath)
			{
				m_DeviatedFromPath = false;
				RestartPathFollowing();
			}
		}

		protected virtual void InitLocalAvoidanceSolvers()
		{
			m_MoversVOsProvider = new MoversVOsProvider(this, m_Config);
			if (m_Config.AvoidStaticObstacles)
			{
				m_TrianglesVOProvider = new StaticTrianglesVOProvider(this, m_Config.Radius, m_Config.MaxSpeed, m_Config.ORCATau, _ReturnIntersecting: false);
			}
		}
	}
}
