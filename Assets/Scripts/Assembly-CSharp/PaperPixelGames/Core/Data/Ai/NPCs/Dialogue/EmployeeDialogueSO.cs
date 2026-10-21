using System;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Ai.NPCs.Dialogue
{
	[CreateAssetMenu(menuName = "Data/Dialogue/Employee Dialogue", fileName = "EmployeeDialogue")]
	public sealed class EmployeeDialogueSO : ScriptableObject
	{
		[Serializable]
		public struct TwoPartJoke
		{
			[TextArea(1, 4)]
			public string Question;

			[TextArea(1, 6)]
			public string Answer;

			[Header("Button Label (after question)")]
			public string ContinueLabel;

			[NonSerialized]
			public string ContinueLocalizationKey;
		}

		[Header("Identity")]
		public ActorType OwnerActorType;

		[Header("Greetings (Random)")]
		[TextArea(2, 6)]
		public string[] Greetings;

		[Header("Farewells (Random)")]
		[TextArea(2, 6)]
		public string[] Farewells;

		[Header("Two-Part Jokes (Question/Answer)")]
		[SerializeField]
		private TwoPartJoke[] _twoPartJokes;

		public bool HasJokes
		{
			get
			{
				if (_twoPartJokes != null)
				{
					return _twoPartJokes.Length != 0;
				}
				return false;
			}
		}

		public string GetRandomGreeting()
		{
			int num = UnityEngine.Random.Range(0, Greetings.Length);
			return GetLocalizedDialogueValue("greeting", num, null, Greetings[num]);
		}

		public string GetRandomFarewell()
		{
			int num = UnityEngine.Random.Range(0, Farewells.Length);
			return GetLocalizedDialogueValue("farewell", num, null, Farewells[num]);
		}

		public TwoPartJoke GetRandomJoke()
		{
			int num = UnityEngine.Random.Range(0, _twoPartJokes.Length);
			TwoPartJoke result = _twoPartJokes[num];
			result.Question = GetLocalizedDialogueValue("joke", num, "question", result.Question);
			result.Answer = GetLocalizedDialogueValue("joke", num, "answer", result.Answer);
			result.ContinueLabel = GetLocalizedDialogueValue("joke", num, "continue", result.ContinueLabel);
			result.ContinueLocalizationKey = GetDialogueKey("joke", num, "continue");
			return result;
		}

		private string GetLocalizedDialogueValue(string section, int index, string field, string fallback)
		{
			return LocalizationDatabase.GetString("EmployeeDialogueTable", GetDialogueKey(section, index, field), fallback);
		}

		private string GetDialogueKey(string section, int index, string field)
		{
			string arg = LocalizationDatabase.ToSnakeCase(OwnerActorType.ToString());
			string text = $"employee_dialogue.{arg}.{section}.{index + 1:00}";
			if (!string.IsNullOrWhiteSpace(field))
			{
				text = text + "." + field;
			}
			return text;
		}
	}
}
