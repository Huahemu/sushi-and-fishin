using System;
using System.Threading.Tasks;
using Nav3D.API;
using Nav3D.Agents;

namespace Nav3D.Common.AgentCore
{
	public class AgentOrder
	{
		private readonly PathfindingRequest m_Request;

		private Nav3DPath m_Path;

		private Action<Nav3DPath> m_OnPathfindingSuccess;

		private Action<PathfindingResult> m_OnPathfindingFail;

		public MovementLogic MovementLogic { get; }

		public AgentOrder(PathfindingRequest _Request, MovementLogic _MovementLogic)
		{
			m_Request = _Request;
			MovementLogic = _MovementLogic;
		}

		public void Init(Action<Nav3DPath> _OnReady, Action<PathfindingResult> _OnPathfindingFail, Log _Log = null)
		{
			m_OnPathfindingSuccess = _OnReady;
			m_OnPathfindingFail = _OnPathfindingFail;
			m_Path = new Nav3DPath(_Log);
			m_Path.Find(m_Request).ContinueWith(OnPathfindingFinished, TaskContinuationOptions.OnlyOnRanToCompletion);
		}

		public void Cancel()
		{
			m_Path.Dispose();
		}

		private void OnPathfindingFinished(Task<PathfindingResult> _PathfindingTask)
		{
			if (!m_Path.IsValid)
			{
				return;
			}
			if (_PathfindingTask.Result.Failed)
			{
				if (m_OnPathfindingFail != null)
				{
					ThreadDispatcher.BeginInvoke(m_OnPathfindingFail, _PathfindingTask.Result);
				}
			}
			else
			{
				ThreadDispatcher.BeginInvoke(m_OnPathfindingSuccess, m_Path);
			}
		}
	}
}
