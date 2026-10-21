using System;
using System.Collections.Generic;
using System.Reflection;

public static class PredefinedAssemblyUtil
{
	private enum AssemblyType
	{
		AssemblyCSharp = 0,
		AssemblyCSharpEditor = 1,
		AssemblyCSharpEditorFirstPass = 2,
		AssemblyCSharpFirstPass = 3
	}

	private static AssemblyType? GetAssemblyType(string assemblyName)
	{
		return assemblyName switch
		{
			"Assembly-CSharp" => AssemblyType.AssemblyCSharp, 
			"Assembly-CSharp-Editor" => AssemblyType.AssemblyCSharpEditor, 
			"Assembly-CSharp-Editor-firstpass" => AssemblyType.AssemblyCSharpEditorFirstPass, 
			"Assembly-CSharp-firstpass" => AssemblyType.AssemblyCSharpFirstPass, 
			_ => null, 
		};
	}

	private static void AddTypesFromAssembly(Type[] assemblyTypes, Type interfaceType, ICollection<Type> results)
	{
		if (assemblyTypes == null)
		{
			return;
		}
		foreach (Type type in assemblyTypes)
		{
			if (type != interfaceType && interfaceType.IsAssignableFrom(type))
			{
				results.Add(type);
			}
		}
	}

	public static List<Type> GetTypes(Type interfaceType)
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		Dictionary<AssemblyType, Type[]> dictionary = new Dictionary<AssemblyType, Type[]>();
		List<Type> list = new List<Type>();
		for (int i = 0; i < assemblies.Length; i++)
		{
			AssemblyType? assemblyType = GetAssemblyType(assemblies[i].GetName().Name);
			if (assemblyType.HasValue)
			{
				dictionary.Add(assemblyType.Value, assemblies[i].GetTypes());
			}
		}
		dictionary.TryGetValue(AssemblyType.AssemblyCSharp, out var value);
		AddTypesFromAssembly(value, interfaceType, list);
		dictionary.TryGetValue(AssemblyType.AssemblyCSharpFirstPass, out var value2);
		AddTypesFromAssembly(value2, interfaceType, list);
		return list;
	}
}
