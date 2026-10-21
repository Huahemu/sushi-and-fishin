using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperPixelGames.Core.Ai.NPCs;
using SushiAndFish.Casino;

public static partial class CityExpansionInstallation
{
    const string CityRoot="Assets/SushiAndFish/CityExpansion";
    public static void InstallDialogue()
    {
        if(!File.Exists(Path.Combine(CityExpansionInspection.Reports,"city-expansion-backup.json")))throw new Exception("A current backup is required.");
        Directory.CreateDirectory(CityRoot+"/Prefabs");AssetDatabase.Refresh();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/Resident_Marina.prefab").GetComponentInChildren<ResidentBillboardText>(true);
        if(!source)throw new Exception("Native shop billboard missing.");
        var copy=UnityEngine.Object.Instantiate(source.gameObject);copy.name="CasinoNativeShopBillboard";
        // Capture the source prefab's world size, including its NPC hierarchy.
        copy.transform.SetParent(null);copy.transform.localScale=source.transform.lossyScale;
        var template=PrefabUtility.SaveAsPrefabAsset(copy,CityRoot+"/Prefabs/CasinoNativeShopBillboard.prefab");
        UnityEngine.Object.DestroyImmediate(copy);
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var casino=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ParkCasinoIntegration>(true)).Single();
        var conversation=casino.GetComponent<CasinoElevatorConversation>();
        if(!conversation)conversation=casino.gameObject.AddComponent<CasinoElevatorConversation>();
        conversation.billboardTemplate=template.GetComponent<ResidentBillboardText>();
        casino.elevatorConversation=conversation;
        EditorUtility.SetDirty(conversation);EditorUtility.SetDirty(casino);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("CITY_NATIVE_ELEVATOR_DIALOGUE_INSTALLED");
    }
    public static void DialogueAndAudit(){InstallDialogue();CityExpansionPlayAudit.Start();}
}
