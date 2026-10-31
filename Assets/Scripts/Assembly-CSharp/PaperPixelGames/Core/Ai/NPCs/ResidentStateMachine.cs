using System;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentStateMachine
	{
		private IResidentState _current;

		private bool _suspended;

        // A local conversation hold must yield before any outside owner
        // changes the state or controls suspension. No callback rewrites state.
        public event Action BeforeExternalControl;

		public IResidentState Current => _current;

		public string CurrentName
		{
			get
			{
				if (_current == null)
				{
					return "None";
				}
				return _current.GetDebugName();
			}
		}

		public bool IsSuspended => _suspended;

		public bool ChangeState(IResidentState next, ResidentRequestPriority priority)
		{
			if (next == null)
			{
				return false;
			}
			if (_current != null && !_current.CanInterrupt(priority))
			{
				return false;
			}
            BeforeExternalControl?.Invoke();
			_current?.Exit();
			_current = next;
			_current.Enter();
			return true;
		}

		public void Tick(float deltaTime)
		{
			if (!_suspended)
			{
				_current?.Tick(deltaTime);
			}
		}

		public void Suspend()
		{
            BeforeExternalControl?.Invoke();
			_suspended = true;
		}

		public void Resume()
		{
            BeforeExternalControl?.Invoke();
			_suspended = false;
		}
	}
}
