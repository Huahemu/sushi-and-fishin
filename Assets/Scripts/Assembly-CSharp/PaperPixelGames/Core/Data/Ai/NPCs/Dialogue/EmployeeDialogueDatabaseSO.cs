using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Ai.NPCs.Dialogue
{
	[CreateAssetMenu(menuName = "Data/Dialogue/Employee Dialogue Database", fileName = "EmployeeDialogueDatabase")]
	public sealed class EmployeeDialogueDatabaseSO : ScriptableObject
	{
		[SerializeField]
		private List<EmployeeDialogueSO> _employees = new List<EmployeeDialogueSO>();

		public bool TryGet(ActorType actorType, out EmployeeDialogueSO employee)
		{
			for (int i = 0; i < _employees.Count; i++)
			{
				EmployeeDialogueSO employeeDialogueSO = _employees[i];
				if (employeeDialogueSO != null && employeeDialogueSO.OwnerActorType == actorType)
				{
					employee = employeeDialogueSO;
					return true;
				}
			}
			employee = null;
			return false;
		}
	}
}
