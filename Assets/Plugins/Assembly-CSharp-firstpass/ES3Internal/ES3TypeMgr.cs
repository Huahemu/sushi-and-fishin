using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using ES3Types;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Scripting;

namespace ES3Internal
{
	[Preserve]
	public static class ES3TypeMgr
	{
		private static object _lock = new object();

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Dictionary<Type, ES3Type> types = null;

		private static ES3Type lastAccessedType = null;

		public static ES3Type GetOrCreateES3Type(Type type, bool throwException = true)
		{
			if (types == null)
			{
				Init();
			}
			if (type != typeof(object) && lastAccessedType != null && lastAccessedType.type == type)
			{
				return lastAccessedType;
			}
			if (types.TryGetValue(type, out lastAccessedType))
			{
				return lastAccessedType;
			}
			return lastAccessedType = CreateES3Type(type, throwException);
		}

		public static ES3Type GetES3Type(Type type)
		{
			if (types == null)
			{
				Init();
			}
			if (types.TryGetValue(type, out lastAccessedType))
			{
				return lastAccessedType;
			}
			return null;
		}

		internal static void Add(Type type, ES3Type es3Type)
		{
			if (types == null)
			{
				Init();
			}
			ES3Type eS3Type = GetES3Type(type);
			if (eS3Type != null && eS3Type.priority > es3Type.priority)
			{
				return;
			}
			lock (_lock)
			{
				types[type] = es3Type;
			}
		}

		internal static ES3Type CreateES3Type(Type type, bool throwException = true)
		{
			if (ES3Reflection.IsEnum(type))
			{
				return new ES3Type_enum(type);
			}
			ES3Type eS3Type;
			if (ES3Reflection.TypeIsArray(type))
			{
				switch (ES3Reflection.GetArrayRank(type))
				{
				case 1:
					eS3Type = new ES3ArrayType(type);
					break;
				case 2:
					eS3Type = new ES32DArrayType(type);
					break;
				case 3:
					eS3Type = new ES33DArrayType(type);
					break;
				default:
					if (throwException)
					{
						throw new NotSupportedException("Only arrays with up to three dimensions are supported by Easy Save.");
					}
					return null;
				}
			}
			else if (ES3Reflection.IsGenericType(type) && ES3Reflection.ImplementsInterface(type, typeof(IEnumerable)))
			{
				Type genericTypeDefinition = ES3Reflection.GetGenericTypeDefinition(type);
				if (typeof(List<>).IsAssignableFrom(genericTypeDefinition))
				{
					eS3Type = new ES3ListType(type);
				}
				else if (typeof(Dictionary<, >).IsAssignableFrom(genericTypeDefinition))
				{
					eS3Type = new ES3DictionaryType(type);
				}
				else if (genericTypeDefinition == typeof(Queue<>))
				{
					eS3Type = new ES3QueueType(type);
				}
				else if (genericTypeDefinition == typeof(Stack<>))
				{
					eS3Type = new ES3StackType(type);
				}
				else if (genericTypeDefinition == typeof(HashSet<>))
				{
					eS3Type = new ES3HashSetType(type);
				}
				else if (genericTypeDefinition == typeof(NativeArray<>))
				{
					eS3Type = new ES3NativeArrayType(type);
				}
				else if ((eS3Type = GetES3Type(genericTypeDefinition)) != null)
				{
					eS3Type = GetGenericES3Type(type, throwException);
				}
			}
			else
			{
				if (ES3Reflection.IsPrimitive(type))
				{
					if (types == null || types.Count == 0)
					{
						throw new TypeLoadException("ES3Type for primitive could not be found, and the type list is empty. Please contact Easy Save developers at https://www.moodkie.com/contact");
					}
					throw new TypeLoadException("ES3Type for primitive could not be found, but the type list has been initialised and is not empty. Please contact Easy Save developers using the form at https://www.moodkie.com/contact.");
				}
				if ((eS3Type = GetGenericES3Type(type, throwException: false)) == null)
				{
					eS3Type = (ES3Reflection.IsAssignableFrom(typeof(UnityEngine.Component), type) ? new ES3ReflectedComponentType(type) : (ES3Reflection.IsAssignableFrom(typeof(ScriptableObject), type) ? new ES3ReflectedScriptableObjectType(type) : (ES3Reflection.IsAssignableFrom(typeof(UnityEngine.Object), type) ? new ES3ReflectedUnityObjectType(type) : ((!ES3Reflection.IsValueType(type)) ? ((ES3Type)new ES3ReflectedObjectType(type)) : ((ES3Type)new ES3ReflectedValueType(type))))));
				}
			}
			if (eS3Type == null || eS3Type.type == null || eS3Type.isUnsupported)
			{
				if (throwException)
				{
					throw new NotSupportedException($"ES3Type.type is null when trying to create an ES3Type for {type}, possibly because the element type is not supported.");
				}
				return null;
			}
			Add(type, eS3Type);
			return eS3Type;
		}

		private static ES3Type GetGenericES3Type(Type type, bool throwException)
		{
			if (!ES3Reflection.IsGenericType(type))
			{
				return null;
			}
			if (type.Name.StartsWith("Tuple`"))
			{
				return new ES3TupleType(type);
			}
			Type genericTypeDefinition = ES3Reflection.GetGenericTypeDefinition(type);
			ES3Type eS3Type = GetES3Type(genericTypeDefinition);
			if (eS3Type != null)
			{
				ConstructorInfo constructor = ES3Reflection.GetConstructor(eS3Type.GetType(), new Type[1] { typeof(Type) });
				if (constructor == null)
				{
					if (throwException)
					{
						throw new NotSupportedException($"Generic type {type} is not supported by Easy Save as it's generic type definition {genericTypeDefinition} does not have a constructor which accepts a Type as a parameter.");
					}
					return null;
				}
				return (ES3Type)constructor.Invoke(new object[1] { type });
			}
			if (throwException)
			{
				throw new NotSupportedException("Generic type \"" + type.ToString() + "\" is not supported by Easy Save.");
			}
			return null;
		}

		internal static void Init()
		{
			lock (_lock)
			{
				types = new Dictionary<Type, ES3Type>();
				ES3Reflection.GetInstances<ES3Type>();
				if (types == null || types.Count == 0)
				{
					throw new TypeLoadException("Type list could not be initialised. Please contact Easy Save developers on mail@moodkie.com.");
				}
			}
		}
	}
}
