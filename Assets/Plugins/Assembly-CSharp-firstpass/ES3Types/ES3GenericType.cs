using System;
using ES3Internal;
using UnityEngine.Scripting;

namespace ES3Types
{
	[Preserve]
	public abstract class ES3GenericType : ES3Type
	{
		public Type[] genericArguments;

		public ES3Type[] genericArgumentES3Types;

		public ES3Type argumentES3Type;

		public Type genericArgument;

		private ES3Reflection.ES3ReflectedMember hasValueProperty;

		private ES3Reflection.ES3ReflectedMember valueProperty;

		public ES3GenericType(Type type)
			: base(type)
		{
			genericArguments = ES3Reflection.GetGenericArguments(type);
			genericArgumentES3Types = new ES3Type[genericArguments.Length];
			for (int i = 0; i < genericArguments.Length; i++)
			{
				genericArgumentES3Types[i] = ES3TypeMgr.GetOrCreateES3Type(genericArguments[i], throwException: false);
				if (genericArgumentES3Types[i] == null || genericArgumentES3Types[i].isUnsupported)
				{
					isUnsupported = true;
				}
			}
		}

		public override void Write(object obj, ES3Writer writer)
		{
			bool flag = (bool)hasValueProperty.GetValue(obj);
			writer.WriteProperty("HasValue", flag, ES3Type_bool.Instance);
			if (flag)
			{
				object value = valueProperty.GetValue(obj);
				writer.WriteProperty("Value", value, argumentES3Type);
			}
		}

		public override object Read<T>(ES3Reader reader)
		{
			if (!reader.ReadProperty<bool>(ES3Type_bool.Instance))
			{
				return ES3Reflection.GetConstructor(type, new Type[0]).Invoke(new object[0]);
			}
			object obj = reader.ReadProperty<object>(argumentES3Type);
			return ES3Reflection.GetConstructor(type, new Type[1] { genericArgument }).Invoke(new object[1] { obj });
		}
	}
}
