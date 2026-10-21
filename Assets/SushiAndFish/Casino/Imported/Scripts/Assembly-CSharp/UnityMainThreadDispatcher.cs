namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Concurrent;
using global::UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
	private static UnityMainThreadDispatcher _inst;

	private readonly ConcurrentQueue<Action> _jobs = new ConcurrentQueue<Action>();

	public static void Enqueue(Action job)
	{
		if (_inst == null)
		{
			Debug.LogError("⚠\ufe0f  Add a UnityMainThreadDispatcher GameObject in the first scene!");
		}
		else
		{
			_inst._jobs.Enqueue(job);
		}
	}

	private void Awake()
	{
		if (_inst != null)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		_inst = this;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
	}

	private void Update()
	{
		Action result;
		while (_jobs.TryDequeue(out result))
		{
			result?.Invoke();
		}
	}
}

}
