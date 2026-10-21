namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::TMPro;
using global::UnityEngine;

public class ReasonDemo : MonoBehaviour
{
	[SerializeField]
	private WordBankSO wordBank;

	[Range(1f, 3f)]
	public int maxSlots = 3;

	[SerializeField]
	private TextMeshProUGUI exampleOutput;

	private List<SentencePattern> _patterns;

	private void Awake()
	{
		_patterns = new List<SentencePattern>
		{
			new SentencePattern("1: we + Conseq", new PatternToken[2]
			{
				PatternToken.Lit("we"),
				PatternToken.Slot(SlotType.Conseq)
			}),
			new SentencePattern("2: Verb Object", new PatternToken[2]
			{
				PatternToken.Slot(SlotType.Verb),
				PatternToken.Slot(SlotType.Object)
			}),
			new SentencePattern("2: Verb Opponent", new PatternToken[2]
			{
				PatternToken.Slot(SlotType.Verb),
				PatternToken.Slot(SlotType.Opponent)
			}),
			new SentencePattern("2: Verb at Location", new PatternToken[3]
			{
				PatternToken.Slot(SlotType.Verb),
				PatternToken.Lit("at"),
				PatternToken.Slot(SlotType.Location)
			}),
			new SentencePattern("3: Verb Object and Conseq", new PatternToken[4]
			{
				PatternToken.Slot(SlotType.Verb),
				PatternToken.Slot(SlotType.Object),
				PatternToken.Lit("and"),
				PatternToken.Slot(SlotType.Conseq)
			}),
			new SentencePattern("3: Verb Object at Location", new PatternToken[4]
			{
				PatternToken.Slot(SlotType.Verb),
				PatternToken.Slot(SlotType.Object),
				PatternToken.Lit("at"),
				PatternToken.Slot(SlotType.Location)
			}),
			new SentencePattern("3: Verb Object with Opponent", new PatternToken[4]
			{
				PatternToken.Slot(SlotType.Verb),
				PatternToken.Slot(SlotType.Object),
				PatternToken.Lit("with"),
				PatternToken.Slot(SlotType.Opponent)
			}),
			new SentencePattern("3: we + Conseq at Location", new PatternToken[4]
			{
				PatternToken.Lit("we"),
				PatternToken.Slot(SlotType.Conseq),
				PatternToken.Lit("at"),
				PatternToken.Slot(SlotType.Location)
			}),
			new SentencePattern("3: Verb Opponent and Conseq", new PatternToken[4]
			{
				PatternToken.Slot(SlotType.Verb),
				PatternToken.Slot(SlotType.Opponent),
				PatternToken.Lit("and"),
				PatternToken.Slot(SlotType.Conseq)
			})
		};
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.R))
		{
			GenerateExampleOutput();
		}
	}

	private void GenerateExampleOutput()
	{
		SentencePattern pattern = PickPatternByCap(maxSlots);
		string text = SentenceGenerator.BuildLine(wordBank, pattern, maxSlots);
		exampleOutput.text = text;
	}

	private static int CountSlots(SentencePattern p)
	{
		int num = 0;
		foreach (PatternToken token in p.tokens)
		{
			if (!token.isLiteral)
			{
				num++;
			}
		}
		return num;
	}

	private SentencePattern PickPatternByCap(int maxSlots)
	{
		List<SentencePattern> list = _patterns.FindAll((SentencePattern p) => CountSlots(p) <= maxSlots);
		if (list.Count == 0)
		{
			return _patterns[0];
		}
		return list[Random.Range(0, list.Count)];
	}
}

}
