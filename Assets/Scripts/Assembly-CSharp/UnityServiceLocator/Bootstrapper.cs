using UnityEngine;

namespace UnityServiceLocator
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(ServiceLocator))]
	public abstract class Bootstrapper : MonoBehaviour
	{
		private ServiceLocator container;

		private bool hasBeenBootstrapped;

		internal ServiceLocator Container => container.OrNull() ?? (container = GetComponent<ServiceLocator>());

		private void Awake()
		{
			BootstrapOnDemand();
		}

		public void BootstrapOnDemand()
		{
			if (!hasBeenBootstrapped)
			{
				hasBeenBootstrapped = true;
				Bootstrap();
			}
		}

		protected abstract void Bootstrap();
	}
}
