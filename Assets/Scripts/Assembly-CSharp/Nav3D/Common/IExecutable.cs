using System;
using System.Threading;

namespace Nav3D.Common
{
	public interface IExecutable
	{
		CancellationToken CancellationToken { get; }

		void Execute(Action _OnStart, Action _OnResolve);

		string GetExecutingStatus();
	}
}
