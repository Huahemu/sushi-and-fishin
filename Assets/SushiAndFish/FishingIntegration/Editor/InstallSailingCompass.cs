using System;
using UnityEditor;
using UnityEngine;
using SushiAndFish.FishingIntegration;
public static class InstallSailingCompass {
    const string Root="Assets/HowToFish/Textures/Navigation/";
    public static void ApplyAssets(){
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before importing original navigation art");
        AssetDatabase.Refresh();var config=MigrationConfig.Load();
        config.compassCircle=Sprite("Circle");config.compassFace=Sprite("UICircle");config.compassDot=Sprite("Dot");config.compassArrow=Sprite("Arrow");Sprite("UICircle_outline");EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
    }
    static Sprite Sprite(string name){string path=Root+name+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new Exception("Original HowToFish texture missing: "+path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);}
}
