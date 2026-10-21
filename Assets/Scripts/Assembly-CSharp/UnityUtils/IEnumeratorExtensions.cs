using System.Collections;
using System.Threading.Tasks;

namespace UnityUtils
{
	public static class IEnumeratorExtensions
	{
		public static IEnumerator AsCoroutine(this Task task)
		{
			while (!task.IsCompleted)
			{
				yield return null;
			}
			task.GetAwaiter().GetResult();
		}
	}
}
