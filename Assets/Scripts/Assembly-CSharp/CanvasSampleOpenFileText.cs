using System;
using System.Collections;
using SFB;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CanvasSampleOpenFileText : MonoBehaviour, IPointerDownHandler, IEventSystemHandler
{
	public Text output;

	public void OnPointerDown(PointerEventData eventData)
	{
	}

	[Obsolete]
	private void Start()
	{
		GetComponent<Button>().onClick.AddListener(OnClick);
	}

	[Obsolete]
	private void OnClick()
	{
		string[] array = StandaloneFileBrowser.OpenFilePanel("Title", "", "txt", multiselect: false);
		if (array.Length != 0)
		{
			StartCoroutine(OutputRoutine(new Uri(array[0]).AbsoluteUri));
		}
	}

	[Obsolete]
	private IEnumerator OutputRoutine(string url)
	{
		WWW loader = new WWW(url);
		yield return loader;
		output.text = loader.text;
	}
}
