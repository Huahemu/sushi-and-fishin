using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Bozo.ModularCharacters
{
	public class CharacterSpinner : MonoBehaviour, IDragHandler, IEventSystemHandler, IPointerDownHandler, IPointerUpHandler
	{
		public CharacterCreator CharacterCreator;

		public float spinDir;

		public GameObject character;

		private Animator anim;

		private float dizzyTimer = 1f;

		private bool spinning;

		private void OnEnable()
		{
			if ((bool)CharacterCreator)
			{
				CharacterCreator characterCreator = CharacterCreator;
				characterCreator.onCharacterChanged = (UnityAction<GameObject>)Delegate.Combine(characterCreator.onCharacterChanged, new UnityAction<GameObject>(SetCharacter));
			}
		}

		private void OnDisable()
		{
			if ((bool)CharacterCreator)
			{
				CharacterCreator characterCreator = CharacterCreator;
				characterCreator.onCharacterChanged = (UnityAction<GameObject>)Delegate.Remove(characterCreator.onCharacterChanged, new UnityAction<GameObject>(SetCharacter));
			}
		}

		public void SetCharacter(GameObject character)
		{
			this.character = character;
			anim = character.GetComponentInChildren<Animator>();
		}

		public void OnDrag(PointerEventData eventData)
		{
			spinDir = (0f - eventData.delta.x) * 0.1f;
			character.transform.Rotate(0f, spinDir, 0f);
			dizzyTimer = 0.5f;
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			spinning = true;
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			spinning = false;
			OnDrag(eventData);
		}

		private void Start()
		{
			SetCharacter(character);
		}

		private void Update()
		{
			if (dizzyTimer <= 0f)
			{
				if (spinDir >= 5f || spinDir <= -5f)
				{
					anim.SetBool("Dizzy", value: true);
				}
				else
				{
					anim.SetBool("Dizzy", value: false);
				}
			}
			if (!spinning)
			{
				character.transform.Rotate(0f, spinDir, 0f);
				spinDir = Mathf.Lerp(spinDir, 0f, Time.deltaTime);
				dizzyTimer -= Time.deltaTime;
			}
		}
	}
}
