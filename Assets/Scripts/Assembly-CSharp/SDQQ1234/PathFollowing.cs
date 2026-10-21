using System.Collections.Generic;
using UnityEngine;

namespace SDQQ1234
{
	public class PathFollowing : MonoBehaviour
	{
		public List<Transform> pathPoints = new List<Transform>();

		private int currentPoint;

		private bool isReturning;

		public float moveSpeed = 10f;

		private void Update()
		{
			if (pathPoints.Count == 0)
			{
				return;
			}
			if (Vector3.Distance(base.transform.position, pathPoints[currentPoint].position) < 0.1f)
			{
				if (!isReturning)
				{
					if (currentPoint < pathPoints.Count - 1)
					{
						currentPoint++;
					}
					else
					{
						isReturning = true;
						currentPoint--;
					}
				}
				else if (currentPoint > 0)
				{
					currentPoint--;
				}
				else
				{
					isReturning = false;
					currentPoint++;
				}
			}
			base.transform.position = Vector3.MoveTowards(base.transform.position, pathPoints[currentPoint].position, Time.deltaTime * moveSpeed);
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.blue;
			Gizmos.DrawWireSphere(base.transform.position, 0.3f);
			if (pathPoints.Count > 1)
			{
				for (int i = 0; i < pathPoints.Count - 1; i++)
				{
					Gizmos.color = Color.yellow;
					Gizmos.DrawSphere(pathPoints[i].position, 0.2f);
					Gizmos.color = Color.green;
					Gizmos.DrawLine(pathPoints[i].position, pathPoints[i + 1].position);
				}
			}
		}
	}
}
