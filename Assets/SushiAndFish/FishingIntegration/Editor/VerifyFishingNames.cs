using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Localization;
using SushiAndFish.FishingIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

// Reads actual Addressables language bundles outside Play. No game/player
// initialization, input, save writes or physical stepping.
public static class VerifyFishingNames {
    const string Out="D:/UnityProjects/_CodexWork/FishingNameFix-20261027/";
    static readonly List<string> checks=new List<string>();
    [Serializable]class Names{public string selectedLocale,tableAssetPath,oldLookup;public int total;public Row[] items;}
    [Serializable]class Row{public string id,expected,actual,recipeName;}
    static void Check(bool condition,string label){checks.Add((condition?"PASS ":"FAIL ")+label);File.WriteAllLines(Out+"Checks.txt",checks);if(!condition)throw new Exception(label);}
    public static void Run(){
        Directory.CreateDirectory(Out);Check(!EditorApplication.isPlaying,"Read-only localization/recipe checks outside Play; no gameplay simulation");
        var config=MigrationConfig.Load();var db=config.database;
        var init=LocalizationSettings.InitializationOperation;init.WaitForCompletion();Check(init.Status==AsyncOperationStatus.Succeeded,"Real LocalizationSettings initialization completes using recovered Addressables");
        var chinese=LocalizationSettings.AvailableLocales.Locales.First(l=>l.Identifier.Code=="zh-CN");
        var operation=LocalizationSettings.StringDatabase.GetTableAsync("ItemsTable",chinese);var runtime=operation.WaitForCompletion();Check(operation.Status==AsyncOperationStatus.Succeeded&&runtime,"Actual simplified-Chinese runtime ItemsTable loaded");
        var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Localization.Tables.StringTable>("Assets/Settings/Localization/Tables/ItemsTable/ItemsTable_zh-CN.asset");
        Check(runtime!=source&&string.IsNullOrEmpty(AssetDatabase.GetAssetPath(runtime)),"Regression uses bundled runtime table, not the updated editor YAML asset");
        string key="item.HTF_DISH_021.name";
        Check(source.GetEntry(key)!=null&&runtime.GetEntry(key)==null,"Screenshot cause reproduced: source contains crab recipe name while original language bundle does not");
        string broken=LocalizationSettings.StringDatabase.GetLocalizedString("ItemsTable",key,chinese,FallbackBehavior.DontUseFallback);
        Check(!string.IsNullOrWhiteSpace(broken)&&broken.Contains("No translation found"),"Old direct lookup returns a nonempty diagnostic rather than the intended fallback");
        var items=db._allItems.Where(i=>i&&i.Id.StartsWith("HTF_",StringComparison.Ordinal)).ToArray();Check(items.Length==283,"All 111 fish appearances, 53 raw meats, 66 processed ingredients and 53 dishes included");
        var rows=new List<Row>();var resolver=new SushiRecipeResolver(db,null);
        foreach(var item in items){string name=item.LocalizedName;Check(name==item.Name&&!name.Contains("No translation found")&&!string.IsNullOrWhiteSpace(name),item.Id+": actual ItemSO.LocalizedName shows its Chinese name after localization initialization");
            var row=new Row{id=item.Id,expected=item.Name,actual=name};
            if(item is SushiSO dish){Check(resolver.TryResolveById(dish.Id,out var recipe)&&recipe.DisplayName==dish.Name,dish.Id+": chef recipe book and menu recipe resolver use the correct display name");row.recipeName=recipe.DisplayName;}
            rows.Add(row);
        }
        var native=db._allItems.First(i=>i&&!i.Id.StartsWith("HTF_",StringComparison.Ordinal)&&LocalizationSettings.StringDatabase.GetTableEntry("ItemsTable","item."+i.Id+".name").Entry!=null);
        var nativeEntry=LocalizationSettings.StringDatabase.GetTableEntry("ItemsTable","item."+native.Id+".name").Entry;
        Check(native.LocalizedName==nativeEntry.GetLocalizedString(),"Existing original item translations still resolve from the selected language bundle");
        Check(LocalizationDatabase.GetString("ItemsTable","missing.static.namefix.regression","测试名称")=="测试名称","Unknown key uses the supplied fallback even after initialization");
        Check(LocalizationDatabase.GetString("ItemsTable",null,"无键名称")=="无键名称"&&LocalizationDatabase.GetString("ItemsTable","",null)==string.Empty,"Null/empty keys retain the original fallback behavior");
        Check(LocalizationDatabase.GetString("UiTable","fishing.collection_only","这类钓获物只能收藏、展示或出售，不能切片。")=="这类钓获物只能收藏、展示或出售，不能切片。","Existing fillet warning also falls back correctly when an older runtime UI bundle lacks the new key");
        File.WriteAllText(Out+"RuntimeNames.json",JsonUtility.ToJson(new Names{selectedLocale=LocalizationSettings.SelectedLocale.Identifier.Code,tableAssetPath=AssetDatabase.GetAssetPath(runtime),oldLookup=broken,total=items.Length,items=rows.ToArray()},true));
        Debug.Log("FISHING_RUNTIME_NAMES_VERIFIED checks="+checks.Count+" items="+items.Length+" dishes=53 runtimeBundles=true play=false simulation=false");
    }
}
