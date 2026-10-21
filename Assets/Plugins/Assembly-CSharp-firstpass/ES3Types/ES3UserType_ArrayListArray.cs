using System.Collections;

namespace ES3Types
{
	public class ES3UserType_ArrayListArray : ES3ArrayType
	{
		public static ES3Type Instance;

		public ES3UserType_ArrayListArray()
			: base(typeof(ArrayList[]), ES3Type_ArrayList.Instance)
		{
			Instance = this;
		}
	}
}
