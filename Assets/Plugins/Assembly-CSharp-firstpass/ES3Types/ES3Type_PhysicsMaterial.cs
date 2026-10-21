using UnityEngine;
using UnityEngine.Scripting;

namespace ES3Types
{
	[Preserve]
	[ES3Properties(new string[] { "dynamicFriction", "staticFriction", "bounciness", "frictionCombine", "bounceCombine" })]
	public class ES3Type_PhysicsMaterial : ES3ObjectType
	{
		public static ES3Type Instance;

		public ES3Type_PhysicsMaterial()
			: base(typeof(PhysicsMaterial))
		{
			Instance = this;
		}

		protected override void WriteObject(object obj, ES3Writer writer)
		{
			PhysicsMaterial physicsMaterial = (PhysicsMaterial)obj;
			writer.WriteProperty("dynamicFriction", physicsMaterial.dynamicFriction, ES3Type_float.Instance);
			writer.WriteProperty("staticFriction", physicsMaterial.staticFriction, ES3Type_float.Instance);
			writer.WriteProperty("bounciness", physicsMaterial.bounciness, ES3Type_float.Instance);
			writer.WriteProperty("frictionCombine", physicsMaterial.frictionCombine);
			writer.WriteProperty("bounceCombine", physicsMaterial.bounceCombine);
		}

		protected override void ReadObject<T>(ES3Reader reader, object obj)
		{
			PhysicsMaterial physicsMaterial = (PhysicsMaterial)obj;
			foreach (string property in reader.Properties)
			{
				switch (property)
				{
				case "dynamicFriction":
					physicsMaterial.dynamicFriction = reader.Read<float>(ES3Type_float.Instance);
					break;
				case "staticFriction":
					physicsMaterial.staticFriction = reader.Read<float>(ES3Type_float.Instance);
					break;
				case "bounciness":
					physicsMaterial.bounciness = reader.Read<float>(ES3Type_float.Instance);
					break;
				case "frictionCombine":
					physicsMaterial.frictionCombine = reader.Read<PhysicsMaterialCombine>();
					break;
				case "bounceCombine":
					physicsMaterial.bounceCombine = reader.Read<PhysicsMaterialCombine>();
					break;
				default:
					reader.Skip();
					break;
				}
			}
		}

		protected override object ReadObject<T>(ES3Reader reader)
		{
			PhysicsMaterial physicsMaterial = new PhysicsMaterial();
			ReadObject<T>(reader, physicsMaterial);
			return physicsMaterial;
		}
	}
}
