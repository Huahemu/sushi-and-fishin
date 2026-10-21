using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RecoveryFontDiagnostic
{
    [Serializable] private class Row
    {
        public string objectName,text,font,material,shader,atlasPath,materialTexturePath;
        public int declaredWidth,declaredHeight,atlasWidth,atlasHeight,materialTextureWidth,materialTextureHeight;
        public Vector4 uv0,uv1,uv2,uv3;
        public Vector2 scale,offset;
        public string glyphRect;
        public Vector4 glyphPixels;
    }
    [Serializable] private class Report { public List<Row> rows=new List<Row>(); }
    public static void Run() { Capture(false); EditorApplication.Exit(0); }
    public static void CaptureRuntime() { Capture(true); }
    private static void Capture(bool runtime)
    {
        RestoredRenderPipelineSupport.RegisterOriginalAssets();
        if(!runtime)EditorSceneManager.OpenScene("Assets/Scenes/MainMenuState.unity",OpenSceneMode.Single);
        var report=new Report();
        var seen=new HashSet<string>();
        foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(string.IsNullOrEmpty(text.text) || !text.font || report.rows.Count>=100)continue;
            text.ForceMeshUpdate(true,true);
            int chosen=0;
            for(int i=0;i<text.textInfo.characterCount;i++)if(text.textInfo.characterInfo[i].character>=0x2e80){chosen=i;break;}
            var font=text.font; var material=text.fontSharedMaterial;
            if(text.textInfo.characterCount>0)
            {
                var actual=text.textInfo.characterInfo[chosen];
                if(actual.fontAsset)font=actual.fontAsset;
                if(text.textInfo.meshInfo[actual.materialReferenceIndex].material)material=text.textInfo.meshInfo[actual.materialReferenceIndex].material;
            }
            if(!seen.Add(font.name+"|"+(material?material.name:"null")))continue;
            var atlas=font.atlasTexture; var texture=material?material.mainTexture:null;
            var row=new Row { objectName=text.name,text=text.text.Length>80?text.text.Substring(0,80):text.text,
                font=font.name,material=material?material.name:null,shader=material && material.shader?material.shader.name:null,
                declaredWidth=font.atlasWidth,declaredHeight=font.atlasHeight,atlasWidth=atlas?atlas.width:0,atlasHeight=atlas?atlas.height:0,
                materialTextureWidth=texture?texture.width:0,materialTextureHeight=texture?texture.height:0,
                atlasPath=atlas?AssetDatabase.GetAssetPath(atlas):null,materialTexturePath=texture?AssetDatabase.GetAssetPath(texture):null,
                scale=material?material.mainTextureScale:Vector2.zero,offset=material?material.mainTextureOffset:Vector2.zero };
            if(text.textInfo.characterCount>0)
            {
                var ch=text.textInfo.characterInfo[chosen]; int index=ch.vertexIndex,mat=ch.materialReferenceIndex;
                var uv=text.textInfo.meshInfo[mat].uvs0;
                if(uv.Length>index+3){row.uv0=uv[index];row.uv1=uv[index+1];row.uv2=uv[index+2];row.uv3=uv[index+3];}
                if(ch.textElement!=null && ch.textElement.glyph!=null){var r=ch.textElement.glyph.glyphRect;row.glyphRect=r.ToString();row.glyphPixels=new Vector4(r.x,r.y,r.width,r.height);}
            }
            report.rows.Add(row);
        }
        var output=Path.GetFullPath(Path.Combine(Application.dataPath,runtime?"../../../reports/font-uv-material-runtime.json":"../../../reports/font-uv-material-diagnostic.json"));
        File.WriteAllText(output,JsonUtility.ToJson(report,true));
    }
}
