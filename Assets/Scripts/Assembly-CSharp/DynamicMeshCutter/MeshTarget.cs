using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
	public class MeshTarget : MonoBehaviour
	{
		[Tooltip("This represents the root of the object and the gameobject that will be destroyed if \"DestroyTargets\" is enabled in the cutter. ")]
		public GameObject GameobjectRoot;

		public Material OverrideFaceMaterial;

		[Tooltip("Create geometry that closes the open surface produced by a cut.")]
		public bool CreateCutFaces = true;

		public bool SeparateMeshes;

		public bool ApplyTranslation = true;

		public Behaviour[] DefaultBehaviour = new Behaviour[2];

		public DynamicRagdoll DynamicRagdoll;

		public Animator Animator;

		public List<GroupBehaviours> GroupBehaviours = new List<GroupBehaviours>();

		public bool[] CreateRigidbody = new bool[2] { true, true };

		public bool[] CreateMeshCollider = new bool[2] { true, true };

		public RagdollPhysics[] Physics = new RagdollPhysics[2]
		{
			RagdollPhysics.NonKinematic,
			RagdollPhysics.NonKinematic
		};

		public bool[] Inherit = new bool[2];

		private MeshRenderer _meshRenderer;

		private SkinnedMeshRenderer _skinnedMeshRenderer;

		public MeshRenderer MeshRenderer
		{
			get
			{
				if (_meshRenderer == null)
				{
					_meshRenderer = GetComponent<MeshRenderer>();
				}
				return _meshRenderer;
			}
		}

		public SkinnedMeshRenderer SkinnedMeshRenderer
		{
			get
			{
				if (_skinnedMeshRenderer == null)
				{
					_skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
				}
				return _skinnedMeshRenderer;
			}
		}

		public bool IsSkinned
		{
			get
			{
				if (!(SkinnedMeshRenderer != null))
				{
					return false;
				}
				return true;
			}
		}

		public Renderer Renderer
		{
			get
			{
				if (!IsSkinned)
				{
					return MeshRenderer;
				}
				return SkinnedMeshRenderer;
			}
		}

		public Material FaceMaterial
		{
			get
			{
				if (OverrideFaceMaterial != null)
				{
					return OverrideFaceMaterial;
				}
				return TryGetMaterial();
			}
		}

		public bool RequireLocal => GetComponent<SkinnedMeshRenderer>() != null;

		private void OnEnable()
		{
			MeshTargetShephard.RegisterMeshTarget(this);
		}

		private void OnDisable()
		{
			MeshTargetShephard.UnRegisterMeshTarget(this);
		}

		protected virtual void Start()
		{
			if (DynamicRagdoll == null)
			{
				DynamicRagdoll = GetComponentInParent<DynamicRagdoll>();
			}
		}

		public Material TryGetMaterial()
		{
			MeshRenderer component = GetComponent<MeshRenderer>();
			if (component != null)
			{
				return component.material;
			}
			SkinnedMeshRenderer component2 = GetComponent<SkinnedMeshRenderer>();
			if (component2 != null)
			{
				return component2.material;
			}
			return null;
		}
	}
}
