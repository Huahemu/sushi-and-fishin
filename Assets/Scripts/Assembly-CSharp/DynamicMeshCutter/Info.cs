using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
	public class Info
	{
		public MeshTarget MeshTarget;

		public VirtualPlane Plane;

		public Mesh TargetOriginalMesh;

		public VirtualMesh TargetVirtualMesh;

		public Matrix4x4[] Bindposes;

		public VirtualMesh[] CreatedMeshes;

		public int[] Sides;

		public int[] BT;

		public List<Vector3> LocalFaceCenters = new List<Vector3>();

		public OnCut OnCutCallback;

		public OnCreated OnCreatedCallback;

		public object BoxedUserData;

		public Info(MeshTarget target, VirtualPlane plane, OnCut onCut, OnCreated onCreated, object boxed)
		{
			MeshTarget = target;
			Plane = plane;
			OnCutCallback = onCut;
			OnCreatedCallback = onCreated;
			BoxedUserData = boxed;
			MeshCreation.GetMeshInfo(target, out TargetOriginalMesh, out Bindposes);
			TargetVirtualMesh = new VirtualMesh(TargetOriginalMesh);
			if (target.DynamicRagdoll != null)
			{
				TargetVirtualMesh.AssignRagdoll(target.DynamicRagdoll);
			}
		}

		public List<Vector3> GetWorldFaceCenters()
		{
			List<Vector3> list = new List<Vector3>();
			for (int i = 0; i < LocalFaceCenters.Count; i++)
			{
				list.Add(MeshTarget.transform.TransformPoint(LocalFaceCenters[i]));
			}
			return list;
		}
	}
}
