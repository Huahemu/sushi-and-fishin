using System.Collections;
using UnityEngine.Scripting;

namespace ES3Types
{
	[Preserve]
	[ES3Properties(new string[] { "_items", "_size", "_version" })]
	public class ES3Type_ArrayList : ES3ObjectType
	{
		public static ES3Type Instance;

		public ES3Type_ArrayList()
			: base(typeof(ArrayList))
		{
			Instance = this;
		}

		protected override void WriteObject(object obj, ES3Writer writer)
		{
			ArrayList objectContainingField = (ArrayList)obj;
			writer.WritePrivateField("_items", objectContainingField);
			writer.WritePrivateField("_size", objectContainingField);
			writer.WritePrivateField("_version", objectContainingField);
		}

		protected override void ReadObject<T>(ES3Reader reader, object obj)
		{
			ArrayList objectContainingField = (ArrayList)obj;
			foreach (string property in reader.Properties)
			{
				switch (property)
				{
				case "_items":
					objectContainingField = (ArrayList)reader.SetPrivateField("_items", reader.Read<object[]>(), objectContainingField);
					break;
				case "_size":
					objectContainingField = (ArrayList)reader.SetPrivateField("_size", reader.Read<int>(), objectContainingField);
					break;
				case "_version":
					objectContainingField = (ArrayList)reader.SetPrivateField("_version", reader.Read<int>(), objectContainingField);
					break;
				default:
					reader.Skip();
					break;
				}
			}
		}

		protected override object ReadObject<T>(ES3Reader reader)
		{
			ArrayList arrayList = new ArrayList();
			ReadObject<T>(reader, arrayList);
			return arrayList;
		}
	}
}
