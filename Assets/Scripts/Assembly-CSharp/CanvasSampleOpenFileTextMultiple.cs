using System;
using System.Collections;
using System.Collections.Generic;
using SFB;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CanvasSampleOpenFileTextMultiple : MonoBehaviour, IPointerDownHandler, IEventSystemHandler
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
		string[] array = StandaloneFileBrowser.OpenFilePanel("Open File", "", "", multiselect: true);
		if (array.Length != 0)
		{
			List<string> list = new List<string>(array.Length);
			for (int i = 0; i < array.Length; i++)
			{
				list.Add(new Uri(array[i]).AbsoluteUri);
			}
			StartCoroutine(OutputRoutine(list.ToArray()));
		}
	}

	[Obsolete]
	private IEnumerator OutputRoutine(string[] urlArr)
	{
		string outputText = "";
		for (int i = 0; i < urlArr.Length; i++)
		{
			WWW loader = new WWW(urlArr[i]);
			yield return loader;
			outputText += loader.text;
		}
		output.text = outputText;
	}
}
