using UnityEngine;
using UnityEngine.UI;

namespace ZakhanSpellsPack
{
	public class Demo : MonoBehaviour
	{
		public enum T
		{
			Spell = 0,
			Projectiles = 1,
			Aura = 2,
			Shield = 3,
			Variations = 4,
			Buff = 5,
			Tome = 6
		}

		public T demo;

		public GameObject[] SpellList;

		public GameObject[] CastingList;

		public GameObject[] AuraList;

		public GameObject[] ShieldList;

		public GameObject[] VariationsList;

		public GameObject[] BuffList;

		public GameObject[] TomeList;

		public Text Title;

		public int Selection;

		public GameObject BackText;

		public GameObject NextText;

		public GameObject BackButton;

		public GameObject NextButton;

		public GameObject SpellsGroup;

		public GameObject CastingGroup;

		public GameObject AuraGroup;

		public GameObject ShieldGroup;

		public GameObject VariationsGroup;

		public GameObject BuffGroup;

		public GameObject TomeGroup;

		public GameObject SelectionSpells;

		public GameObject SelectionProjectiles;

		public GameObject SelectionAura;

		public GameObject SelectionShields;

		public GameObject SelectionVariations;

		public GameObject SelectionBuff;

		public GameObject SelectionTome;

		private void Start()
		{
			SpellList[Selection].SetActive(value: true);
			Title.text = "Prefab Name: " + SpellList[Selection].gameObject.transform.name.ToString();
		}

		private void Update()
		{
			if (Selection == 0)
			{
				BackText.SetActive(value: false);
				BackButton.SetActive(value: false);
			}
			else
			{
				BackText.SetActive(value: true);
				BackButton.SetActive(value: true);
			}
			if (demo == T.Spell)
			{
				if (Selection == SpellList.Length - 1)
				{
					NextText.SetActive(value: false);
					NextButton.SetActive(value: false);
				}
				else
				{
					NextText.SetActive(value: true);
					NextButton.SetActive(value: true);
				}
				SelectionProjectiles.SetActive(value: false);
				SelectionAura.SetActive(value: false);
				SelectionShields.SetActive(value: false);
				SelectionVariations.SetActive(value: false);
				SelectionSpells.SetActive(value: true);
				SelectionTome.SetActive(value: false);
				SelectionBuff.SetActive(value: false);
			}
			if (demo == T.Projectiles)
			{
				if (Selection == CastingList.Length - 1)
				{
					NextText.SetActive(value: false);
					NextButton.SetActive(value: false);
				}
				else
				{
					NextText.SetActive(value: true);
					NextButton.SetActive(value: true);
				}
				SelectionSpells.SetActive(value: false);
				SelectionAura.SetActive(value: false);
				SelectionShields.SetActive(value: false);
				SelectionVariations.SetActive(value: false);
				SelectionProjectiles.SetActive(value: true);
				SelectionTome.SetActive(value: false);
				SelectionBuff.SetActive(value: false);
			}
			if (demo == T.Aura)
			{
				if (Selection == AuraList.Length - 1)
				{
					NextText.SetActive(value: false);
					NextButton.SetActive(value: false);
				}
				else
				{
					NextText.SetActive(value: true);
					NextButton.SetActive(value: true);
				}
				SelectionSpells.SetActive(value: false);
				SelectionProjectiles.SetActive(value: false);
				SelectionShields.SetActive(value: false);
				SelectionVariations.SetActive(value: false);
				SelectionAura.SetActive(value: true);
				SelectionTome.SetActive(value: false);
				SelectionBuff.SetActive(value: false);
			}
			if (demo == T.Shield)
			{
				if (Selection == ShieldList.Length - 1)
				{
					NextText.SetActive(value: false);
					NextButton.SetActive(value: false);
				}
				else
				{
					NextText.SetActive(value: true);
					NextButton.SetActive(value: true);
				}
				SelectionSpells.SetActive(value: false);
				SelectionProjectiles.SetActive(value: false);
				SelectionAura.SetActive(value: false);
				SelectionVariations.SetActive(value: false);
				SelectionShields.SetActive(value: true);
				SelectionTome.SetActive(value: false);
				SelectionBuff.SetActive(value: false);
			}
			if (demo == T.Variations)
			{
				if (Selection == VariationsList.Length - 1)
				{
					NextText.SetActive(value: false);
					NextButton.SetActive(value: false);
				}
				else
				{
					NextText.SetActive(value: true);
					NextButton.SetActive(value: true);
				}
				SelectionSpells.SetActive(value: false);
				SelectionProjectiles.SetActive(value: false);
				SelectionAura.SetActive(value: false);
				SelectionShields.SetActive(value: false);
				SelectionVariations.SetActive(value: true);
				SelectionTome.SetActive(value: false);
				SelectionBuff.SetActive(value: false);
			}
			if (demo == T.Buff)
			{
				if (Selection == BuffList.Length - 1)
				{
					NextText.SetActive(value: false);
					NextButton.SetActive(value: false);
				}
				else
				{
					NextText.SetActive(value: true);
					NextButton.SetActive(value: true);
				}
				SelectionSpells.SetActive(value: false);
				SelectionProjectiles.SetActive(value: false);
				SelectionAura.SetActive(value: false);
				SelectionShields.SetActive(value: false);
				SelectionVariations.SetActive(value: false);
				SelectionTome.SetActive(value: false);
				SelectionBuff.SetActive(value: true);
			}
			if (demo == T.Tome)
			{
				if (Selection == TomeList.Length - 1)
				{
					NextText.SetActive(value: false);
					NextButton.SetActive(value: false);
				}
				else
				{
					NextText.SetActive(value: true);
					NextButton.SetActive(value: true);
				}
				SelectionSpells.SetActive(value: false);
				SelectionProjectiles.SetActive(value: false);
				SelectionAura.SetActive(value: false);
				SelectionShields.SetActive(value: false);
				SelectionVariations.SetActive(value: false);
				SelectionTome.SetActive(value: true);
				SelectionBuff.SetActive(value: false);
			}
			if (Input.GetKeyDown(KeyCode.A))
			{
				Back();
			}
			if (Input.GetKeyDown(KeyCode.D))
			{
				Next();
			}
		}

		public void Back()
		{
			if (demo == T.Spell && Selection < SpellList.Length && Selection != 0)
			{
				SpellList[Selection].SetActive(value: false);
				Selection--;
				SpellList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + SpellList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Projectiles && Selection < CastingList.Length && Selection != 0)
			{
				CastingList[Selection].SetActive(value: false);
				Selection--;
				CastingList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + CastingList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Aura && Selection < AuraList.Length && Selection != 0)
			{
				AuraList[Selection].SetActive(value: false);
				Selection--;
				AuraList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + AuraList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Shield && Selection < ShieldList.Length && Selection != 0)
			{
				ShieldList[Selection].SetActive(value: false);
				Selection--;
				ShieldList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + ShieldList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Variations && Selection < VariationsList.Length && Selection != 0)
			{
				VariationsList[Selection].SetActive(value: false);
				Selection--;
				VariationsList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + VariationsList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Buff && Selection < BuffList.Length && Selection != 0)
			{
				BuffList[Selection].SetActive(value: false);
				Selection--;
				BuffList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + BuffList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Tome && Selection < TomeList.Length && Selection != 0)
			{
				TomeList[Selection].SetActive(value: false);
				Selection--;
				TomeList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + TomeList[Selection].gameObject.transform.name.ToString();
			}
		}

		public void Next()
		{
			if (demo == T.Spell && Selection < SpellList.Length && Selection != SpellList.Length - 1)
			{
				SpellList[Selection].SetActive(value: false);
				Selection++;
				SpellList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + SpellList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Projectiles && Selection < CastingList.Length && Selection != CastingList.Length - 1)
			{
				CastingList[Selection].SetActive(value: false);
				Selection++;
				CastingList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + CastingList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Aura && Selection < AuraList.Length && Selection != AuraList.Length - 1)
			{
				AuraList[Selection].SetActive(value: false);
				Selection++;
				AuraList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + AuraList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Shield && Selection < ShieldList.Length && Selection != ShieldList.Length - 1)
			{
				ShieldList[Selection].SetActive(value: false);
				Selection++;
				ShieldList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + ShieldList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Variations && Selection < VariationsList.Length && Selection != VariationsList.Length - 1)
			{
				VariationsList[Selection].SetActive(value: false);
				Selection++;
				VariationsList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + VariationsList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Buff && Selection < BuffList.Length && Selection != BuffList.Length - 1)
			{
				BuffList[Selection].SetActive(value: false);
				Selection++;
				BuffList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + BuffList[Selection].gameObject.transform.name.ToString();
			}
			if (demo == T.Tome && Selection < TomeList.Length && Selection != TomeList.Length - 1)
			{
				TomeList[Selection].SetActive(value: false);
				Selection++;
				TomeList[Selection].SetActive(value: true);
				Title.text = "Prefab Name: " + TomeList[Selection].gameObject.transform.name.ToString();
			}
		}

		public void Spells()
		{
			Last();
			demo = T.Spell;
			Selection = 0;
			SpellList[Selection].SetActive(value: true);
			Title.text = "Prefab Name: " + SpellList[Selection].gameObject.transform.name.ToString();
			AuraGroup.SetActive(value: false);
			CastingGroup.SetActive(value: false);
			ShieldGroup.SetActive(value: false);
			VariationsGroup.SetActive(value: false);
			SpellsGroup.SetActive(value: true);
			BuffGroup.SetActive(value: false);
			TomeGroup.SetActive(value: false);
		}

		public void Projectiles()
		{
			Last();
			demo = T.Projectiles;
			Selection = 0;
			CastingList[Selection].SetActive(value: true);
			Title.text = "Prefab Name: " + CastingList[Selection].gameObject.transform.name.ToString();
			AuraGroup.SetActive(value: false);
			SpellsGroup.SetActive(value: false);
			ShieldGroup.SetActive(value: false);
			VariationsGroup.SetActive(value: false);
			CastingGroup.SetActive(value: true);
			BuffGroup.SetActive(value: false);
			TomeGroup.SetActive(value: false);
		}

		public void Auras()
		{
			Last();
			demo = T.Aura;
			Selection = 0;
			AuraList[Selection].SetActive(value: true);
			Title.text = "Prefab Name: " + AuraList[Selection].gameObject.transform.name.ToString();
			CastingGroup.SetActive(value: false);
			SpellsGroup.SetActive(value: false);
			ShieldGroup.SetActive(value: false);
			VariationsGroup.SetActive(value: false);
			AuraGroup.SetActive(value: true);
			BuffGroup.SetActive(value: false);
			TomeGroup.SetActive(value: false);
		}

		public void Shields()
		{
			Last();
			demo = T.Shield;
			Selection = 0;
			ShieldList[Selection].SetActive(value: true);
			Title.text = "Prefab Name: " + ShieldList[Selection].gameObject.transform.name.ToString();
			AuraGroup.SetActive(value: false);
			CastingGroup.SetActive(value: false);
			SpellsGroup.SetActive(value: false);
			VariationsGroup.SetActive(value: false);
			ShieldGroup.SetActive(value: true);
			BuffGroup.SetActive(value: false);
			TomeGroup.SetActive(value: false);
		}

		public void Variations()
		{
			Last();
			demo = T.Variations;
			Selection = 0;
			VariationsList[Selection].SetActive(value: true);
			Title.text = "Prefab Name: " + VariationsList[Selection].gameObject.transform.name.ToString();
			AuraGroup.SetActive(value: false);
			CastingGroup.SetActive(value: false);
			SpellsGroup.SetActive(value: false);
			ShieldGroup.SetActive(value: false);
			VariationsGroup.SetActive(value: true);
			BuffGroup.SetActive(value: false);
			TomeGroup.SetActive(value: false);
		}

		public void Buffs()
		{
			Last();
			demo = T.Buff;
			Selection = 0;
			BuffList[Selection].SetActive(value: true);
			Title.text = "Prefab Name: " + BuffList[Selection].gameObject.transform.name.ToString();
			AuraGroup.SetActive(value: false);
			CastingGroup.SetActive(value: false);
			SpellsGroup.SetActive(value: false);
			ShieldGroup.SetActive(value: false);
			VariationsGroup.SetActive(value: false);
			BuffGroup.SetActive(value: true);
			TomeGroup.SetActive(value: false);
		}

		public void Tomes()
		{
			Last();
			demo = T.Tome;
			Selection = 0;
			TomeList[Selection].SetActive(value: true);
			Title.text = "Prefab Name: " + TomeList[Selection].gameObject.transform.name.ToString();
			AuraGroup.SetActive(value: false);
			CastingGroup.SetActive(value: false);
			SpellsGroup.SetActive(value: false);
			ShieldGroup.SetActive(value: false);
			VariationsGroup.SetActive(value: false);
			BuffGroup.SetActive(value: false);
			TomeGroup.SetActive(value: true);
		}

		public void Last()
		{
			if (Selection < SpellList.Length)
			{
				SpellList[Selection].SetActive(value: false);
			}
			if (Selection < CastingList.Length)
			{
				CastingList[Selection].SetActive(value: false);
			}
			if (Selection < AuraList.Length)
			{
				AuraList[Selection].SetActive(value: false);
			}
			if (Selection < ShieldList.Length)
			{
				ShieldList[Selection].SetActive(value: false);
			}
			if (Selection < VariationsList.Length)
			{
				VariationsList[Selection].SetActive(value: false);
			}
			if (Selection < BuffList.Length)
			{
				BuffList[Selection].SetActive(value: false);
			}
			if (Selection < TomeList.Length)
			{
				TomeList[Selection].SetActive(value: false);
			}
		}
	}
}
