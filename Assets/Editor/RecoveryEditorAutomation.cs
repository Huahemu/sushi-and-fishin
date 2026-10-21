using System;
using System.IO;
using UnityEditor;
using UnityEngine;
// Fixed local verification entry points for this recovery project. No game
// runtime behavior or arbitrary code is accepted by this editor-only helper.
[InitializeOnLoad]
public static class RecoveryEditorAutomation
{
    private static double next;
    private static string Reports=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports"));
    public static bool KeepOpen=>File.Exists(Path.Combine(Reports,"keep-recovery-editor-open.flag"));
    static RecoveryEditorAutomation(){EditorApplication.update+=Update;}
    private static void Update()
    {
        if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        next=EditorApplication.timeSinceStartup+1;
        var path=Path.Combine(Reports,"recovery-verification.request");
        if(!File.Exists(path))return;
        var command=File.ReadAllText(path).Trim();File.Delete(path);
        if(command=="play")RecoveryPlayAudit.StartAndLaunchGame();
        else if(command=="scenes")RecoveryProjectAudit.AuditScenes();
        else if(command=="fonts")RecoveryFontDiagnostic.Run();
        else if(command=="front-end")IslandFrontEndPlayAudit.Start();
        else if(command=="creator-button-repair"){IslandFrontEndBuilder.BuildCreatorConfirm();CreatorButtonRepairAudit.Start();}
        else if(command=="creator-button-finish"){PlayerSettings.productName="Deep Blue Sushi";UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");AssetDatabase.SaveAssets();}
        else if(command=="world-issues")WorldIssuePlayAudit.Start();
        else if(command=="island-final-cleanup")IslandFinalCleanup.Run();
        else if(command=="island-ocean-surface")IslandOceanSurfaceRepair.Run();
        else if(command=="island-ocean-grid")IslandOceanGridRepair.Run();
        else if(command=="island-followup-repair")IslandFollowupRepair.Run();
        else if(command=="island-final-inspect")IslandFinalInspection.Run();
        else if(command=="comprehensive-inspect")IslandComprehensiveRepair.Inspect();
        else if(command=="comprehensive-repair")IslandComprehensiveRepair.Repair();
        else if(command=="world-issues-inspect")WorldIssueFollowup.Inspect();
        else if(command=="world-issues-shaders")WorldIssueFollowup.InspectShaders();
        else if(command=="world-issues-close")EditorApplication.Exit(0);
        else if(command=="world-font-material"){IslandWorldIssues.RepairFontMaterial();WorldIssuePlayAudit.Start();}
        else if(command=="world-font-bind-only")IslandWorldIssues.RepairFontMaterial();
        else if(command=="world-sea-level")IslandWorldIssues.CalibrateSeaLevel();
        else if(command=="refresh")AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        else if(command=="loading-presentation")LoadingScreenPresentationRepair.ApplyAndPreview();
        else if(command=="foundation-diagnose")IslandFoundationDiagnosis.Inspect();
        else if(command=="foundation-gaps")IslandFoundationDiagnosis.InspectGaps();
        else if(command=="foundation-deep")IslandFoundationDiagnosis.InspectDeep();
        else if(command=="foundation-native-heights")IslandFoundationDiagnosis.ProbeNativeHeights();
        else if(command=="foundation-prototype")IslandSolidFoundation.Prototype();
        else if(command=="foundation-prototype-play")IslandFoundationPlayAudit.Start(false);
        else if(command=="foundation-complete")IslandSolidFoundation.Complete();
        else if(command=="foundation-complete-play")IslandFoundationPlayAudit.Start(true);
        else if(command=="front-end-visuals")IslandFrontEndVisualDiagnostic.Run();
        else if(command=="front-end-presentation")IslandFrontEndBuilder.RefreshMenuPresentation();
        else if(command=="font-session-reset")RecoveryFontSessionSupport.Reset();
        else if(command=="island-trim")IslandTrimRuntimeAudit.Start();
        else if(command=="island-trim-navigation")IslandTrimNavigationProbe.Run();
        else if(command=="island-trim-metadata")IslandSceneTrim.FinalizeMetadata();
        else if(command=="island-trim-navigation-repair")IslandTrimNavigationRepair.Run();
        else if(command=="island-trim-shore")IslandTrimShoreCleanup.Run();
        else if(command=="building-footprints")BuildingFootprintBuilder.Run();
        else throw new InvalidOperationException("Unknown bounded recovery verification request");
    }
}
