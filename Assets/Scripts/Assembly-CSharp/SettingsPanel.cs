using UnityEngine;

public class SettingsPanel : MonoBehaviour
{
	private RectTransform m_rectTransform;

	private float m_xValueOpen;

	private float m_xValueClosed;

	private void Awake()
	{
		m_rectTransform = GetComponent<RectTransform>();
		m_xValueClosed = 0f - m_rectTransform.sizeDelta.x;
		m_xValueOpen = m_rectTransform.anchoredPosition.x;
	}

	private void Start()
	{
	}

	private void LateUpdate()
	{
		if (Cursor.lockState == CursorLockMode.Locked)
		{
			m_rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(m_rectTransform.anchoredPosition.x, m_xValueClosed, Time.deltaTime * 10f), m_rectTransform.anchoredPosition.y);
		}
		else
		{
			m_rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(m_rectTransform.anchoredPosition.x, m_xValueOpen, Time.deltaTime * 10f), m_rectTransform.anchoredPosition.y);
		}
	}
}
