using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityServiceLocator
{
	public class ServiceManager
	{
		private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

		public IEnumerable<object> RegisteredServices => services.Values;

		public bool TryGet<T>(out T service) where T : class
		{
			Type typeFromHandle = typeof(T);
			if (services.TryGetValue(typeFromHandle, out var value))
			{
				service = value as T;
				return true;
			}
			service = null;
			return false;
		}

		public T Get<T>() where T : class
		{
			Type typeFromHandle = typeof(T);
			if (services.TryGetValue(typeFromHandle, out var value))
			{
				return value as T;
			}
			throw new ArgumentException("ServiceManager.Get: Service of type " + typeFromHandle.FullName + " not registered");
		}

		public ServiceManager Register<T>(T service)
		{
			Type typeFromHandle = typeof(T);
			if (!services.TryAdd(typeFromHandle, service))
			{
				Debug.LogError("ServiceManager.Register: Service of type " + typeFromHandle.FullName + " already registered");
			}
			return this;
		}

		public ServiceManager Register(Type type, object service)
		{
			if (!type.IsInstanceOfType(service))
			{
				throw new ArgumentException("Type of service does not match type of service interface", "service");
			}
			if (!services.TryAdd(type, service))
			{
				Debug.LogError("ServiceManager.Register: Service of type " + type.FullName + " already registered");
			}
			return this;
		}
	}
}
