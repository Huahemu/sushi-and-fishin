using System.Collections.Generic;
using UnityEngine;

namespace HoaxGames
{
	public class CharacterClone : MonoBehaviour
	{
		[SerializeField]
		protected Animator m_animatorTarget;

		[SerializeField]
		private List<string> m_animatorFloatParamsToUpdate;

		[SerializeField]
		private List<string> m_animatorBoolParamsToUpdate;

		protected Transform m_transformTarget;

		protected Transform m_transformSource;

		protected Animator m_animatorSource;

		protected List<int> m_animFloatParamsHashes = new List<int>();

		protected List<int> m_animBoolParamsHashes = new List<int>();

		private void Awake()
		{
			m_transformTarget = m_animatorTarget.transform;
			m_transformSource = base.transform;
			m_animatorSource = GetComponent<Animator>();
		}

		private void Start()
		{
			foreach (string item in m_animatorFloatParamsToUpdate)
			{
				m_animFloatParamsHashes.Add(Animator.StringToHash(item));
			}
			foreach (string item2 in m_animatorBoolParamsToUpdate)
			{
				m_animBoolParamsHashes.Add(Animator.StringToHash(item2));
			}
		}

		private void LateUpdate()
		{
			m_transformSource.position = m_transformTarget.position;
			m_transformSource.rotation = m_transformTarget.rotation;
			foreach (int animFloatParamsHash in m_animFloatParamsHashes)
			{
				m_animatorSource.SetFloat(animFloatParamsHash, m_animatorTarget.GetFloat(animFloatParamsHash));
			}
			foreach (int animBoolParamsHash in m_animBoolParamsHashes)
			{
				m_animatorSource.SetBool(animBoolParamsHash, m_animatorTarget.GetBool(animBoolParamsHash));
			}
		}
	}
}
