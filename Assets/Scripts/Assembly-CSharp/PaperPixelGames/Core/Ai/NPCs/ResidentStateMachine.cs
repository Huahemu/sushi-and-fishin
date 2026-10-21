namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentStateMachine
	{
		private IResidentState _current;

		private bool _suspended;

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
			_suspended = true;
		}

		public void Resume()
		{
			_suspended = false;
		}
	}
}
