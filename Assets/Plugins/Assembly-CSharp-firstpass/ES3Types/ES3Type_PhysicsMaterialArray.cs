using UnityEngine;

namespace ES3Types
{
	public class ES3Type_PhysicsMaterialArray : ES3ArrayType
	{
		public static ES3Type Instance;

		public ES3Type_PhysicsMaterialArray()
			: base(typeof(PhysicsMaterial[]), ES3Type_PhysicsMaterial.Instance)
		{
			Instance = this;
		}
	}
}
