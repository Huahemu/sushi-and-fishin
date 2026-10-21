using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

// The recovered player-only TMP assembly has no corresponding editor lifecycle
// extension. Clear dead dynamic atlases after Play; keep imported font assets.
[InitializeOnLoad]
public static class RecoveryFontSessionSupport
{
    [Serializable] class Row {public string path;public int staleAtlasReferencesBefore,staleAtlasReferencesAfter;}
    [Serializable] class Report {public string utc;public Row[] fonts;}
    static RecoveryFontSessionSupport(){EditorApplication.playModeStateChanged+=OnState;}
    static void OnState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Reset;
    }
    static int Stale(TMP_FontAsset font)
    {
        var array=new SerializedObject(font).FindProperty("m_AtlasTextures");int count=0;
        for(int i=0;i<array.arraySize;i++)if(!array.GetArrayElementAtIndex(i).objectReferenceValue)count++;
        return count;
    }
    public static void Reset()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
        var rows=new List<Row>();
        foreach(var path in AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath))
        {
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(!font)continue;
            var row=new Row{path=path,staleAtlasReferencesBefore=Stale(font)};
            if(row.staleAtlasReferencesBefore>0 && font.atlasPopulationMode!=AtlasPopulationMode.Static
                && File.ReadAllText(path).Contains("m_GlyphTable: []") && font.atlasTextures.Length>0 && font.atlasTextures[0])
                font.ClearFontAssetData();
            row.staleAtlasReferencesAfter=Stale(font);rows.Add(row);
        }
        File.WriteAllText(Path.Combine(IslandFrontEndInventory.Reports,"font-editor-session-reset.json"),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("o"),fonts=rows.ToArray()},true));
    }
}
