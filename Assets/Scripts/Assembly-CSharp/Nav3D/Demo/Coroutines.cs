using System;
using System.Collections;
using UnityEngine;

namespace Nav3D.Demo
{
	public static class Coroutines
	{
		public static IEnumerator DoOnDelay(Action _Delegate, float _Delay)
		{
			yield return new WaitForSeconds(_Delay);
			_Delegate?.Invoke();
		}
	}
}
