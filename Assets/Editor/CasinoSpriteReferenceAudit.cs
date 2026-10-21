using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CasinoSpriteReferenceAudit
{
    public static void Inspect()
    {
        const string root="Assets/SushiAndFish/Casino/Imported/";
        var lines=new System.Collections.Generic.List<string>();
        foreach(var path in new[]{root+"Resources/Casino/Atlas_0.asset",root+"Sprite/NumberButton_Outline@2x.asset"})
        {
            var asset=AssetDatabase.LoadMainAssetAtPath(path);var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            lines.Add("ASSET "+path+" main="+(asset?asset.GetType().FullName+" "+asset.name:"NULL")+" sprite="+(sprite?sprite.name:"NULL"));
            foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path))if(obj){AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id);lines.Add("OBJECT "+obj.GetType().Name+" "+obj.name+" guid="+guid+" fileID="+id);}
        }
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Resources/Casino/resources/SlotMachine CasinoGame.prefab");
        foreach(var image in prefab.GetComponentsInChildren<Image>(true).Take(12))
        {
            var prop=new SerializedObject(image).FindProperty("m_Sprite");var sprite=image.sprite;
            lines.Add("PREFAB "+image.name+" sprite="+(sprite?sprite.name:"MISSING")+" refID="+prop.objectReferenceInstanceIDValue+" texture="+(sprite&&sprite.texture?sprite.texture.name:"NULL"));
        }
        File.WriteAllLines(Path.Combine(IslandRevision20261018.Reports,"casino-sprite-reference-audit.txt"),lines);
    }
}
