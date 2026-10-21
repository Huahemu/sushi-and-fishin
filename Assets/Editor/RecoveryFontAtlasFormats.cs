using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
// Recover the published Alpha8 storage contract used by TMP's writable atlases.
[InitializeOnLoad]
public static class RecoveryFontAtlasFormats
{
    [Serializable] private class SourceRow { public string name; public int width,height,format,mip_count; public bool readable; }
    [Serializable] private class SourceReport { public SourceRow[] rows; }
    [Serializable] private class Row { public string path,beforeFormat,afterFormat; public bool readable; }
    [Serializable] private class Report { public List<Row> rows=new List<Row>(); public bool allAlpha8=true; }
    private static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
    static RecoveryFontAtlasFormats(){EditorApplication.delayCall+=TryRun;}
    private static void TryRun()
    {
        var trigger=Path.Combine(Root,"reports/restore-original-font-atlases.request");
        if(!File.Exists(trigger))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=TryRun;return;}
        File.Delete(trigger);Restore();
        if(!Application.isBatchMode)EditorApplication.delayCall+=RecoveryPlayAudit.StartAndLaunchGame;
    }
    public static void Restore()
    {
        var source=JsonUtility.FromJson<SourceReport>(File.ReadAllText(Path.Combine(Root,"reports/original-font-atlas-formats.json")));
        var done=new HashSet<string>();var report=new Report();
        foreach(var row in source.rows)
        foreach(var file in Directory.GetFiles("Assets/Texture2D",row.name+"*.png"))
        {
            string stem=Path.GetFileNameWithoutExtension(file);
            if(stem!=row.name && !System.Text.RegularExpressions.Regex.IsMatch(stem,"^"+System.Text.RegularExpressions.Regex.Escape(row.name)+"_[0-9]+$"))continue;
            string path=file.Replace('\\','/');if(!done.Add(path))continue;
            if(row.format!=1)throw new InvalidOperationException("Unexpected original font texture format");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var receipt=new Row { path=path,beforeFormat=texture.format.ToString(),readable=row.readable };
            string backup=Path.Combine(Root,"backup/font-atlas-importers-before-alpha8",path+".meta");
            Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(path+".meta",backup);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.sRGBTexture=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=false;importer.isReadable=row.readable;importer.mipmapEnabled=row.mip_count>1;
            var platform=importer.GetPlatformTextureSettings("Standalone");platform.name="Standalone";platform.overridden=true;
            platform.format=TextureImporterFormat.Alpha8;platform.textureCompression=TextureImporterCompression.Uncompressed;
            platform.maxTextureSize=Math.Max(32,Mathf.NextPowerOfTwo(Math.Max(row.width,row.height)));
            importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();
            texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);receipt.afterFormat=texture.format.ToString();
            report.allAlpha8 &= texture.format==TextureFormat.Alpha8;report.rows.Add(receipt);
        }
        File.WriteAllText(Path.Combine(Root,"reports/font-atlas-original-format-restoration.json"),JsonUtility.ToJson(report,true));
        if(!report.allAlpha8)throw new InvalidOperationException("One or more TMP atlases did not retain original Alpha8 storage");
    }
}
