using System.Collections.Generic;
using Nav3D.Common;
using Nav3D.Obstacles.Serialization;
using UnityEngine;

namespace Nav3D.Obstacles
{
	public abstract class ObstacleInfoSingle : ObstacleInfoBase
	{
		private int m_ObstacleControllerID;

		public int ObstacleControllerID => m_ObstacleControllerID;

		public override List<int> IDs => new List<int> { m_ObstacleControllerID };

		public ObstacleInfoSingle(int _ObstacleControllerID)
		{
			m_ObstacleControllerID = _ObstacleControllerID;
		}

		public override void ReplaceID(int _OldID, int _NewID)
		{
			if (m_ObstacleControllerID == _OldID)
			{
				m_ObstacleControllerID = _NewID;
			}
		}

		public ObstacleInfoBaseSerializable GetSerializableInstance(int _ID)
		{
			return new ObstacleInfoSingleSerializable(this, _ID);
		}

		protected abstract List<Triangle> GetTriangles(Vector3 _Position);

		protected void ObtainTriangles(Vector3 _Position)
		{
			base.Triangles = GetTriangles(_Position);
			ComputeBounds();
		}
	}
}
