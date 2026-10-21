namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::DG.Tweening;
using global::FMODUnity;
using global::TMPro;
using global::UnityEngine;

public class DaySummaryTicketEntry : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI sourceText;

	[SerializeField]
	private TextMeshProUGUI ticketsText;

	[SerializeField]
	private EventReference ticketTextChangeSfx;

	[SerializeField]
	private EventReference smallTextSlideSfx;

	private string _source;

	private string _tickets;

	public void Setup(string source, int tickets)
	{
		_source = source;
		_tickets = "+" + tickets;
	}

	public IEnumerator Animate(float duration)
	{
		sourceText.text = _source;
		SFXManager.SFXOneShot(smallTextSlideSfx);
		yield return new WaitForSeconds(duration);
		ticketsText.text = _tickets;
		ticketsText.transform.DOPunchScale(ticketsText.transform.localScale * 0.2f, 0.5f, 1);
		SFXManager.SFXOneShot(ticketTextChangeSfx);
		yield return new WaitForSeconds(0.5f);
	}

	public void SetImmediate()
	{
		sourceText.text = _source;
		ticketsText.text = _tickets;
	}
}

}
