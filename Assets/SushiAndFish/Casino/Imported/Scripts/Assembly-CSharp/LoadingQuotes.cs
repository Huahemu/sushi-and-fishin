namespace SushiAndFish.Casino.Original
{
using global::TMPro;
using global::UnityEngine;

public class LoadingQuotes : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI targetText;

	[SerializeField]
	private string[] quotes;

	public void ShowRandomQuote()
	{
		if (!(targetText == null) && quotes != null && quotes.Length != 0)
		{
			int num = Random.Range(0, quotes.Length);
			targetText.text = quotes[num];
		}
	}
}

}
