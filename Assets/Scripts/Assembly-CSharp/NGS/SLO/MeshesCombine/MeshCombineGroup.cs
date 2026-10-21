using System.Collections.Generic;
using NGS.SLO.Shared;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	public class MeshCombineGroup
	{
		private List<MeshCombineInstance> _instances;

		private MeshCombineOptions _options;

		private int _vertexCount;

		public IList<MeshCombineInstance> Instances => _instances;

		public MeshCombineOptions CombineOptions => _options;

		public int VertexCount => _vertexCount;

		public MeshCombineGroup(MeshCombineOptions options)
		{
			_instances = new List<MeshCombineInstance>();
			_options = options;
		}

		public bool CanAddInstance(MeshCombineInstance instance)
		{
			if (_instances.Count == 0)
			{
				return true;
			}
			if (_instances[0].renderer.gameObject.layer != instance.renderer.gameObject.layer)
			{
				return false;
			}
			Material sharedMaterial = _instances[0].renderer.GetSharedMaterial(_instances[0].submeshIndex);
			Material sharedMaterial2 = instance.renderer.GetSharedMaterial(instance.submeshIndex);
			if (sharedMaterial != sharedMaterial2)
			{
				return false;
			}
			if (!MeshCombineInstancesComparer.HasEqualParameters(_instances[0], instance))
			{
				return false;
			}
			if (_options.limit65kVertices && _vertexCount + instance.vertexCount >= 65535)
			{
				return false;
			}
			return true;
		}

		public bool TryAddInstance(MeshCombineInstance instance)
		{
			if (!CanAddInstance(instance))
			{
				return false;
			}
			_instances.Add(instance);
			_vertexCount += instance.vertexCount;
			return true;
		}
	}
}
