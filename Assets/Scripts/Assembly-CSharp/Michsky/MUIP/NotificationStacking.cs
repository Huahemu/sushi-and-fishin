using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Michsky.MUIP
{
	[AddComponentMenu("Modern UI Pack/Notification/Notification Stacking")]
	public class NotificationStacking : MonoBehaviour
	{
		[Header("Settings")]
		public float delay = 1f;

		private List<NotificationManager> notifications = new List<NotificationManager>();

		private int currentNotification;

		private bool enableUpdating;

		private void Update()
		{
			if (notifications.Count != 0 && enableUpdating && notifications[currentNotification] != null)
			{
				notifications[currentNotification].Open();
				StopCoroutine("StartNotification");
				StartCoroutine("StartNotification");
				enableUpdating = false;
			}
		}

		public void AddToStack(NotificationManager notif)
		{
			notifications.Add(notif);
			StopCoroutine("StartNotification");
			StartCoroutine("StartNotification");
		}

		private IEnumerator StartNotification()
		{
			yield return new WaitForSecondsRealtime(notifications[currentNotification].timer + delay);
			Object.Destroy(notifications[currentNotification].gameObject);
			if (currentNotification == notifications.Count - 1)
			{
				notifications.Clear();
				currentNotification = 0;
			}
			else
			{
				currentNotification++;
				enableUpdating = true;
			}
		}
	}
}
