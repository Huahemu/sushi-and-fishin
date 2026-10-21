using System;
using System.Collections.Generic;
using System.Reflection;
using Deform;
using UnityEngine;

namespace PaperPixelGames.Framework.Utility
{
	public static class DeformableManagerResetUtility
	{
		private const BindingFlags InstancePrivateFlags = BindingFlags.Instance | BindingFlags.NonPublic;

		private static readonly FieldInfo DeformablesField = typeof(DeformableManager).GetField("deformables", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly FieldInfo ImmediateDeformablesField = typeof(DeformableManager).GetField("immediateDeformables", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly FieldInfo AddedDeformablesField = typeof(DeformableManager).GetField("addedDeformables", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly List<IDeformable> TempDeformables = new List<IDeformable>();

		public static void ResetDefaultManager()
		{
			DeformableManager defaultManager = DeformableManager.GetDefaultManager(createIfMissing: false);
			if (!(defaultManager == null))
			{
				ResetManager(defaultManager);
			}
		}

		public static void PruneInvalidDefaultManagerDeformables()
		{
			DeformableManager defaultManager = DeformableManager.GetDefaultManager(createIfMissing: false);
			if (!(defaultManager == null))
			{
				PruneInvalidManagerDeformables(defaultManager);
			}
		}

		public static void PruneInvalidManagerDeformables(DeformableManager manager)
		{
			if (!(manager == null))
			{
				PruneInvalidSet(manager, DeformablesField);
				PruneInvalidSet(manager, ImmediateDeformablesField);
				PruneInvalidSet(manager, AddedDeformablesField);
			}
		}

		public static void ResetManager(DeformableManager manager)
		{
			if (manager == null)
			{
				return;
			}
			bool update = manager.update;
			manager.update = false;
			try
			{
				ResetSet(manager, DeformablesField, "deformables");
				ResetSet(manager, ImmediateDeformablesField, "immediateDeformables");
				ResetSet(manager, AddedDeformablesField, "addedDeformables");
			}
			catch (Exception arg)
			{
				UtilityMethods.Log("<b><color=#FF4040>[Deform]</color></b> Failed to reset DeformableManager." + $"\n   Exception: {arg}", LogType.Error);
			}
			finally
			{
				manager.update = update;
				TempDeformables.Clear();
			}
		}

		private static void ResetSet(DeformableManager manager, FieldInfo field, string fieldName)
		{
			if (field == null)
			{
				UtilityMethods.Log("<b><color=#FFB020>[Deform]</color></b> Reset skipped. Field not found: " + fieldName, LogType.Warning);
			}
			else
			{
				if (!(field.GetValue(manager) is HashSet<IDeformable> { Count: not 0 } hashSet))
				{
					return;
				}
				TempDeformables.Clear();
				TempDeformables.AddRange(hashSet);
				for (int i = 0; i < TempDeformables.Count; i++)
				{
					IDeformable deformable = TempDeformables[i];
					if (IsValidDeformable(deformable))
					{
						try
						{
							deformable.Complete();
							deformable.ApplyData();
						}
						catch (Exception arg)
						{
							UtilityMethods.Log("<b><color=#FFB020>[Deform]</color></b> Failed to complete deformable during reset.\n   Field: " + fieldName + $"\n   Exception: {arg}", LogType.Warning);
						}
					}
				}
				hashSet.Clear();
				TempDeformables.Clear();
				UtilityMethods.Log("<b><color=#8FD14F>[Deform]</color></b> Cleared DeformableManager set: " + fieldName);
			}
		}

		private static bool IsValidDeformable(IDeformable deformable)
		{
			if (deformable == null)
			{
				return false;
			}
			if (deformable is UnityEngine.Object obj && obj == null)
			{
				return false;
			}
			return true;
		}

		private static void PruneInvalidSet(DeformableManager manager, FieldInfo field)
		{
			if (field == null || !(field.GetValue(manager) is HashSet<IDeformable> { Count: not 0 } hashSet))
			{
				return;
			}
			TempDeformables.Clear();
			TempDeformables.AddRange(hashSet);
			for (int i = 0; i < TempDeformables.Count; i++)
			{
				IDeformable deformable = TempDeformables[i];
				if (!IsValidDeformable(deformable))
				{
					hashSet.Remove(deformable);
				}
			}
			TempDeformables.Clear();
		}
	}
}
