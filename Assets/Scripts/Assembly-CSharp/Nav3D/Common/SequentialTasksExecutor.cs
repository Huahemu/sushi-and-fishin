using System;
using System.Threading.Tasks;

namespace Nav3D.Common
{
	public class SequentialTasksExecutor
	{
		private Task m_CurrentTask;

		public Task AddTask(Action _ToDo, TaskContinuationOptions _TaskContinuationOptions = TaskContinuationOptions.None)
		{
			Task result;
			if (m_CurrentTask != null)
			{
				result = (m_CurrentTask = m_CurrentTask.ContinueWith((Task _PreviousTask) =>
				{
					_PreviousTask?.Dispose();
					_ToDo?.Invoke();
				}, _TaskContinuationOptions));
			}
			else
			{
				m_CurrentTask = (result = Task.Factory.StartNew(() =>
				{
					_ToDo?.Invoke();
				}, (TaskCreationOptions)_TaskContinuationOptions)).ContinueWith((Task _Task) =>
				{
					_Task.Dispose();
				}, TaskContinuationOptions.ExecuteSynchronously);
			}
			return result;
		}

		public Task<TResult> AddTask<TResult>(Func<TResult> _ToDo, TaskContinuationOptions _TaskContinuationOptions = TaskContinuationOptions.None)
		{
			Task<TResult> result;
			if (m_CurrentTask != null)
			{
				result = (Task<TResult>)(m_CurrentTask = m_CurrentTask.ContinueWith((Task _Task) =>
				{
					_Task?.Dispose();
					return _ToDo();
				}, _TaskContinuationOptions));
			}
			else
			{
				m_CurrentTask = (result = Task.Factory.StartNew(() => _ToDo(), (TaskCreationOptions)_TaskContinuationOptions)).ContinueWith((Task<TResult> _Task) =>
				{
					_Task.Dispose();
				}, TaskContinuationOptions.ExecuteSynchronously);
			}
			return result;
		}
	}
}
