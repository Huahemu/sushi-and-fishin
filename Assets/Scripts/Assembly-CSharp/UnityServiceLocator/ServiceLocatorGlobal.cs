using UnityEngine;

namespace UnityServiceLocator
{
	[AddComponentMenu("ServiceLocator/ServiceLocator Global")]
	public class ServiceLocatorGlobal : Bootstrapper
	{
		[SerializeField]
		private bool dontDestroyOnLoad = true;

		protected override void Bootstrap()
		{
			base.Container.ConfigureAsGlobal(dontDestroyOnLoad);
		}
	}
}
