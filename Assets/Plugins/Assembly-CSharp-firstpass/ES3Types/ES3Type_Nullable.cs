using System;
using ES3Internal;
using UnityEngine.Scripting;

namespace ES3Types
{
	[Preserve]
	public class ES3Type_Nullable : ES3Type
	{
		public ES3Type argumentES3Type;

		public Type genericArgument;

		private ES3Reflection.ES3ReflectedMember hasValueProperty;

		private ES3Reflection.ES3ReflectedMember valueProperty;

		public ES3Type_Nullable()
			: base(typeof(Nullable<>))
		{
		}

		public ES3Type_Nullable(Type type)
			: base(type)
		{
			hasValueProperty = ES3Reflection.GetES3ReflectedProperty(type, "HasValue");
			valueProperty = ES3Reflection.GetES3ReflectedProperty(type, "Value");
			genericArgument = ES3Reflection.GetGenericArguments(type)[0];
			argumentES3Type = ES3TypeMgr.GetOrCreateES3Type(genericArgument, throwException: false);
			isUnsupported = argumentES3Type == null;
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
