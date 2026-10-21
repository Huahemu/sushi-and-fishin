using System;
using NGS.SLO.MaterialsCombine;
using NGS.SLO.Shared;
using Unity.Jobs;
using UnityEngine;

internal class RecoveredLegacyJobReflectionRegistration
{
	public static void CreateJobReflectionData()
	{
		try
		{
			IJobParallelForExtensions.EarlyJobInit<TexturePacker.FillPixelsBlackColorJob>();
			IJobParallelForExtensions.EarlyJobInit<TexturePacker.ApplyTextureToAtlasJob>();
			IJobExtensions.EarlyJobInit<TexturePacker.FillPaddingJob>();
			IJobParallelForExtensions.EarlyJobInit<TextureUtil.TextureOffsetJob>();
			IJobParallelForExtensions.EarlyJobInit<TextureUtil.TextureRepeatJob>();
			IJobParallelForExtensions.EarlyJobInit<TextureUtil.TextureResizeJob>();
			IJobParallelForExtensions.EarlyJobInit<TextureUtil.TextureRepeatAndResizeJob>();
		}
		catch (Exception ex)
		{
			EarlyInitHelpers.JobReflectionDataCreationFailed(ex);
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
	public static void EarlyInit()
	{
		CreateJobReflectionData();
	}
}
