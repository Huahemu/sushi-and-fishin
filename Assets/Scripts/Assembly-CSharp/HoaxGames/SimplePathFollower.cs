using System.Collections.Generic;
using UnityEngine;

namespace HoaxGames
{
	public class SimplePathFollower : MonoBehaviour
	{
		public enum Direction
		{
			FORWARD = 0,
			BACKWARD = 1
		}

		public class Node
		{
			public Node next;

			public Node prev;

			public Transform transform;

			public Node(Node _next, Node _prev, Transform _transform)
			{
				next = _next;
				prev = _prev;
				transform = _transform;
			}
		}

		[SerializeField]
		private Transform m_pathTarget;

		[SerializeField]
		private bool m_rotateTowardsPathNodeRotation;

		[SerializeField]
		private Direction m_direction;

		[SerializeField]
		private int m_startAtNodeIdx;

		[SerializeField]
		[Range(0f, 10f)]
		private int m_followSpeed = 2;

		private Transform m_transform;

		private List<Node> m_path = new List<Node>();

		private Node m_targetNode;

		private void Awake()
		{
			m_transform = base.transform;
			for (int i = 0; i < m_pathTarget.childCount; i++)
			{
				m_path.Add(new Node(null, null, m_pathTarget.GetChild(i)));
			}
			for (int j = 0; j < m_path.Count; j++)
			{
				int num = j - 1;
				int num2 = j + 1;
				if (num < 0)
				{
					num = m_pathTarget.childCount - 1;
				}
				if (num2 >= m_pathTarget.childCount)
				{
					num2 = 0;
				}
				Node node = m_path[j];
				node.prev = m_path[num];
				node.next = m_path[num2];
			}
			m_targetNode = m_path[m_startAtNodeIdx];
		}

		private void Start()
		{
		}

		private void Update()
		{
			Vector3 vector = m_targetNode.transform.position - m_transform.position;
			m_transform.position += vector.normalized * m_followSpeed * Time.deltaTime;
			Vector3 vector2 = vector - Vector3.Project(vector, Vector3.up);
			if (!m_rotateTowardsPathNodeRotation)
			{
				m_transform.rotation = Quaternion.Slerp(m_transform.rotation, Quaternion.LookRotation(vector2.normalized, Vector3.up), Time.deltaTime * 5f);
			}
			else
			{
				m_transform.rotation = Quaternion.Slerp(m_transform.rotation, m_targetNode.transform.rotation, Time.deltaTime * 5f);
			}
			if (vector.magnitude < (float)m_followSpeed * Time.deltaTime * 5f)
			{
				if (m_direction == Direction.FORWARD)
				{
					m_targetNode = m_targetNode.next;
				}
				else
				{
					m_targetNode = m_targetNode.prev;
				}
			}
		}
	}
}
