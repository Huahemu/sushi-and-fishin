#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SushiAndFish.FriendsLife.Editor
{
    public static class FriendUiTemplateBuilder
    {
        const string DirectoryPath="Assets/SushiAndFish/FriendsLife/Presentation/Resources/FriendsLife";
        const string Design="F:/虚拟现实/MyProject/Sushi & Fish/design/朋友生活完整设计稿";
        [MenuItem("Sushi & Fish/Friends Life/Install Original UI Templates")]
        public static string Install()
        {
            Directory.CreateDirectory(DirectoryPath+"/Templates");AssetDatabase.Refresh();
            var skin=AssetDatabase.LoadAssetAtPath<FriendUiSkin>(DirectoryPath+"/FriendUiSkin.asset");if(!skin){skin=ScriptableObject.CreateInstance<FriendUiSkin>();AssetDatabase.CreateAsset(skin,DirectoryPath+"/FriendUiSkin.asset");}
            skin.paper=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SushiAndFish/ResidentialIsland/Resources/Housing/PaperPanel.asset");
            skin.primary=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/Button_Red.asset");skin.close=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/CloseX.asset");skin.toolSlot=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/TabletToolSlot_UI.asset");skin.userIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/User Filled.asset");
            skin.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MonoBehaviour/NotoSansSC-Bold SDF.asset");
            var fallback=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SushiAndFish/UIFonts/ChineseInterfaceFallback.asset");if(skin.font&&fallback&&!skin.font.fallbackFontAssetTable.Contains(fallback)){skin.font.fallbackFontAssetTable.Add(fallback);EditorUtility.SetDirty(skin.font);}
            skin.graphicMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/SushiAndFish/ResidentialIsland/Resources/Housing/TabletGraphic.mat");
            var blue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/Button_Blue.asset");string secondary=DirectoryPath+"/ButtonBlueTemplateSliced.asset";skin.secondary=AssetDatabase.LoadAssetAtPath<Sprite>(secondary);
            if(!skin.secondary){skin.secondary=Sprite.Create(blue.texture,new Rect(0,0,1716,592),new Vector2(.5f,.5f),blue.pixelsPerUnit,0,SpriteMeshType.FullRect,new Vector4(237,165,237,165));skin.secondary.name="ButtonBlueTemplateSliced";AssetDatabase.CreateAsset(skin.secondary,secondary);}
            EditorUtility.SetDirty(skin);AssetDatabase.SaveAssets();
            string dialogueTarget=DirectoryPath+"/CNzh原文目录.json";if(!File.Exists(dialogueTarget)){File.Copy(Path.Combine(Design,"原对话/CNzh原文目录.json"),dialogueTarget);AssetDatabase.ImportAsset(dialogueTarget,ImportAssetOptions.ForceSynchronousImport);}
            const string avatarSource="F:/虚拟现实/MyProject/Tomodachi-Life-Recovery/Tomodachi-Asset-Workbench/Assets/LifeMigration/Resources/Avatar";
            const string avatarTarget="Assets/SushiAndFish/FriendsLife/Frozen/Data/Resources/Avatar";Directory.CreateDirectory(avatarTarget);
            foreach(string file in new[]{"catalog.json","sdk-presets.json","face-layout.json","face-runtime.json"})if(!File.Exists(avatarTarget+"/"+file)){File.Copy(avatarSource+"/"+file,avatarTarget+"/"+file);AssetDatabase.ImportAsset(avatarTarget+"/"+file,ImportAssetOptions.ForceSynchronousImport);}
            var document=JObject.Parse(File.ReadAllText(Path.Combine(Design,"UI模板/模板合同.json")));
            foreach(var row in document["templates"])
            {
                string id=(string)row["id"];var root=new GameObject(id,typeof(RectTransform));var rect=(RectTransform)root.transform;var size=row["size"]as JArray;
                rect.sizeDelta=size!=null&&size.Count==2?new Vector2((float)size[0],(float)size[1]):new Vector2(1200,720);rect.pivot=new Vector2(0,1);rect.anchorMin=rect.anchorMax=new Vector2(0,1);
                Transform paper=root.transform;
                if(id!="T00_TabletApp"&&id!="T07_FishDialogue"&&id!="T10_PortraitRow"&&id!="T11_MapReturn")paper=FriendUi.Image("Paper",root.transform,0,0,rect.sizeDelta.x,rect.sizeDelta.y,skin.paper).transform;
                if(id=="T00_TabletApp"){FriendUi.Image("Icon",paper,10,10,80,80,skin.userIcon,false);FriendUi.Text("Caption",paper,0,100,100,40,"朋友");}
                else if(id=="T07_FishDialogue")
                {var r=FriendUi.Rect("PanelRoot",paper,225,400,750,300);var buttons=FriendUi.Rect("ButtonsRoot",r,0,0,750,300);var b=FriendUi.Button("PurchaseButton",buttons,0,0,750,45,"",null);var image=b.GetComponent<Image>();image.sprite=null;image.color=new Color(0,0,0,.6f);}
                else if(id=="T11_MapReturn"){rect.sizeDelta=new Vector2(208,54);FriendUi.Button("ReturnFriends",paper,0,0,208,54,"返回朋友",null);FriendUi.Text("KeyGlyph",paper,170,0,38,54,"Esc",20);}
                else
                {
                    foreach(var property in ((JObject)row).Properties())
                    {
                        if(!(property.Value is JArray values)||values.Count!=4||values.Any(v=>v.Type!=JTokenType.Integer&&v.Type!=JTokenType.Float))continue;
                        string name=char.ToUpperInvariant(property.Name[0])+property.Name.Substring(1);float x=(float)values[0],y=(float)values[1],w=(float)values[2],h=(float)values[3];
                        if(name=="Search")FriendUi.Input(name,paper,x,y,w,h,"","搜索");
                        else if(new[]{"Add","Back","Confirm","HomeButton","Upgrade","Arrangement","PersonOpen","Open","Close"}.Contains(name))FriendUi.Button(name,paper,x,y,w,h,name=="Add"?"添加朋友":name=="HomeButton"?"查看住宅":name=="Upgrade"?"升级":name=="Arrangement"?"同住":name=="Confirm"?"确认":"返回",null);
                        else if(new[]{"List","Content","ScrollRows","Fields"}.Contains(name))FriendUi.Scroll(name,paper,x,y,w,h,out _);
                        else if(name.IndexOf("Preview",StringComparison.OrdinalIgnoreCase)>=0||name=="Portrait"||name=="PersonA"||name=="PersonB"){var image=FriendUi.Rect(name,paper,x,y,w,h).gameObject.AddComponent<RawImage>();image.raycastTarget=false;}
                        else FriendUi.Text(name,paper,x,y,w,h,"",name=="Header"||name=="StepTitle"?30:24);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,DirectoryPath+"/Templates/"+id+".prefab");UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();return "Installed 13 original-style templates and 25662-message original CNzh catalogue.";
        }
    }
}
#endif
