using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityServiceLocator
{
	public class ServiceLocator : MonoBehaviour
	{
		private static ServiceLocator global;

		private static Dictionary<Scene, ServiceLocator> sceneContainers;

		private static List<GameObject> tmpSceneGameObjects;

		private readonly ServiceManager services = new ServiceManager();

		private const string k_globalServiceLocatorName = "ServiceLocator [Global]";

		private const string k_sceneServiceLocatorName = "ServiceLocator [Scene]";

		public static ServiceLocator Global
		{
			get
			{
				if (global != null)
				{
					return global;
				}
				ServiceLocatorGlobal serviceLocatorGlobal = UnityEngine.Object.FindFirstObjectByType<ServiceLocatorGlobal>();
				if ((object)serviceLocatorGlobal != null)
				{
					serviceLocatorGlobal.BootstrapOnDemand();
					return global;
				}
				new GameObject("ServiceLocator [Global]", typeof(ServiceLocator)).AddComponent<ServiceLocatorGlobal>().BootstrapOnDemand();
				return global;
			}
		}

		internal void ConfigureAsGlobal(bool dontDestroyOnLoad)
		{
			if (global == this)
			{
				Debug.LogWarning("ServiceLocator.ConfigureAsGlobal: Already configured as global", this);
				return;
			}
			if (global != null)
			{
				Debug.LogError("ServiceLocator.ConfigureAsGlobal: Another ServiceLocator is already configured as global", this);
				return;
			}
			global = this;
			if (dontDestroyOnLoad)
			{
				UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
			}
		}

		internal void ConfigureForScene()
		{
			Scene scene = base.gameObject.scene;
			if (sceneContainers.ContainsKey(scene))
			{
				Debug.LogError("ServiceLocator.ConfigureForScene: Another ServiceLocator is already configured for this scene", this);
			}
			else
			{
				sceneContainers.Add(scene, this);
			}
		}

		public static ServiceLocator ForSceneOf(MonoBehaviour mb)
		{
			Scene scene = mb.gameObject.scene;
			if (sceneContainers.TryGetValue(scene, out var value) && value != mb)
			{
				return value;
			}
			tmpSceneGameObjects.Clear();
			scene.GetRootGameObjects(tmpSceneGameObjects);
			foreach (GameObject item in tmpSceneGameObjects.Where((GameObject go) => go.GetComponent<ServiceLocatorScene>() != null))
			{
				if (item.TryGetComponent<ServiceLocatorScene>(out var component) && component.Container != mb)
				{
					component.BootstrapOnDemand();
					return component.Container;
				}
			}
			return Global;
		}

		public static ServiceLocator For(MonoBehaviour mb)
		{
			return mb.GetComponentInParent<ServiceLocator>().OrNull() ?? ForSceneOf(mb) ?? Global;
		}

		public ServiceLocator Register<T>(T service)
		{
			services.Register(service);
			return this;
		}

		public ServiceLocator Register(Type type, object service)
		{
			services.Register(type, service);
			return this;
		}

		public ServiceLocator Get<T>(out T service) where T : class
		{
			if (TryGetService<T>(out service))
			{
				return this;
			}
			if (TryGetNextInHierarchy(out var container))
			{
				container.Get<T>(out service);
				return this;
			}
			throw new ArgumentException("ServiceLocator.Get: Service of type " + typeof(T).FullName + " not registered");
		}

		public T Get<T>() where T : class
		{
			Type typeFromHandle = typeof(T);
			T service = null;
			if (TryGetService<T>(typeFromHandle, out service))
			{
				return service;
			}
			if (TryGetNextInHierarchy(out var container))
			{
				return container.Get<T>();
			}
			throw new ArgumentException("Could not resolve type '" + typeof(T).FullName + "'.");
		}

		public bool TryGet<T>(out T service) where T : class
		{
			Type typeFromHandle = typeof(T);
			service = null;
			if (TryGetService<T>(typeFromHandle, out service))
			{
				return true;
			}
			if (TryGetNextInHierarchy(out var container))
			{
				return container.TryGet<T>(out service);
			}
			return false;
		}

		private bool TryGetService<T>(out T service) where T : class
		{
			return services.TryGet<T>(out service);
		}

		private bool TryGetService<T>(Type type, out T service) where T : class
		{
			return services.TryGet<T>(out service);
		}

		private bool TryGetNextInHierarchy(out ServiceLocator container)
		{
			if (this == global)
			{
				container = null;
				return false;
			}
			container = base.transform.parent.OrNull()?.GetComponentInParent<ServiceLocator>().OrNull() ?? ForSceneOf(this);
			return container != null;
		}

		private void OnDestroy()
		{
			if (this == global)
			{
				global = null;
			}
			else if (sceneContainers.ContainsValue(this))
			{
				sceneContainers.Remove(base.gameObject.scene);
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			global = null;
			sceneContainers = new Dictionary<Scene, ServiceLocator>();
			tmpSceneGameObjects = new List<GameObject>();
		}
	}
}
