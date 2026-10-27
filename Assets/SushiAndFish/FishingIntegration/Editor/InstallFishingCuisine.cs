using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using HowToFish;
using SushiAndFish.FishingIntegration;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Ai.NPCs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object=UnityEngine.Object;

// Authoring only. All outputs are native IngredientSO, ProcessedIngredientSO
// and SushiSO data. Filleting, cutting, cooking, ordering, serving and sales
// continue through the original game code and original UI without new handlers.
public static class InstallFishingCuisine {
    const string Root="Assets/SushiAndFish/FishingIntegration/Cuisine/";
    const string Out="D:/UnityProjects/_CodexWork/FishingCuisine-20261027/";
    [Serializable]public class Plan{public int version;public Entry[] entries;public Collection[] collectionOnly;public float shinyMultiplier;public int commonSale,uncommonSale,rareSale,legendarySale;}
    [Serializable]public class Entry{public int species;public string dishName,template,rawTemplate;}
    [Serializable]public class Collection{public int species;public string reason;}
    [Serializable]class Report{public int dishes,raw,processed,originalDishes,totalDishes;public Row[] entries;public Sale[] sales;}
    [Serializable]class Row{public int species,price;public string fish,dish,id,kind,template,raw;public string[] ingredients;}
    [Serializable]class Sale{public string id,name;public bool collectionOnly;public float pricePerKg,nominalWeight;public int nominalPrice;}
    static readonly List<string> checks=new List<string>();
    static void Check(bool condition,string label){checks.Add((condition?"PASS ":"FAIL ")+label);File.WriteAllLines(Out+"Checks.txt",checks);if(!condition)throw new Exception(label);}
    static string Number(int id)=>id.ToString("D3");
    static T Read<T>(string path)where T:Object{var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(!asset)throw new Exception("Missing native template: "+path);return asset;}
    static void Folder(string path){path=path.TrimEnd('/');if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
    static T Save<T>(T value,string path)where T:Object{var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing){EditorUtility.CopySerialized(value,existing);Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;}AssetDatabase.CreateAsset(value,path);return value;}
    static void Name(HoldableSO item,string id,string name){item.Id=id;item.name=id;item.Name=name;item.Active=true;var tooltip=item.Tooltip;var data=tooltip.Data;data.Title=name;tooltip.Data=data;item.Tooltip=tooltip;}
    static void Add(ItemDatabaseSO db,ItemSO item,List<ItemSO> category){if(!db._allItems.Contains(item))db._allItems.Add(item);if(!category.Contains(item))category.Add(item);}
    static int Rank(CatchRarity rarity,int common,int uncommon,int rare,int legendary)=>rarity==CatchRarity.Common?common:rarity==CatchRarity.Uncommon?uncommon:rarity==CatchRarity.Rare?rare:legendary;
    static Plan LoadPlan()=>JsonUtility.FromJson<Plan>(Read<TextAsset>(Root+"CuisinePlan.json").text);
    [MenuItem("Tools/Sushi and Fish/Install Native Fish Cuisine")]
    public static void Apply(){ApplyAssets();Verify();}
    public static void ApplyAssets(){
        Directory.CreateDirectory(Out);if(EditorApplication.isPlaying)throw new Exception("Cuisine authoring requires stopped Play mode");
        var plan=LoadPlan();if(plan.version!=1||plan.entries.Length!=53)throw new Exception("Invalid reviewed cuisine plan");
        var localeBefore=LocaleSnapshots();
        var config=MigrationConfig.Load();var db=config.database;Folder(Root+"Ingredients");Folder(Root+"Processed");Folder(Root+"Dishes");
        foreach(var entry in plan.entries){
            var binding=config.Find(entry.species);var template=Read<SushiSO>(entry.template);var rawSource=Read<IngredientSO>(entry.rawTemplate);
            var map=new Dictionary<string,ProcessedIngredientSO>();int index=0;
            foreach(var cutSource in rawSource.ProcessedIngredients){
                var cut=Object.Instantiate(cutSource);string id="HTF_CUT_"+Number(entry.species)+"_"+index++;
                Name(cut,id,binding.definition.displayName+(cut.ProcessedType==ProcessedType.LongStrip?"肉条":"切片"));
                cut=Save(cut,Root+"Processed/"+id+".asset");map.Add(cutSource.Id,cut);Add(db,cut,db._processedIngredientItems);
            }
            var raw=Object.Instantiate(rawSource);Name(raw,"HTF_RAW_"+Number(entry.species),binding.definition.displayName+"净肉");
            raw.ProcessedIngredients=rawSource.ProcessedIngredients.Select(p=>map[p.Id]).ToList();raw=Save(raw,Root+"Ingredients/"+raw.Id+".asset");Add(db,raw,db._rawIngredientItems);
            var dish=Object.Instantiate(template);Name(dish,"HTF_DISH_"+Number(entry.species),entry.dishName);
            dish.Recipe=new Recipe{
                Ingredients=template.Recipe.Ingredients.Select(n=>new IngredientNeeds{Order=n.Order,Amount=n.Amount,Data=n.Data==rawSource?raw:n.Data}).ToList(),
                ProcessedIngredients=template.Recipe.ProcessedIngredients.Select(n=>new ProcessedIngredientNeeds{Order=n.Order,Amount=n.Amount,Data=map.TryGetValue(n.Data.Id,out var replacement)?replacement:n.Data}).ToList()
            };
            dish.Price=Mathf.Max(template.Price,Rank(binding.definition.rarity,18,38,85,180));
            dish=Save(dish,Root+"Dishes/"+dish.Id+".asset");Add(db,dish,db._servedDishItems);
            binding.edible=true;binding.ingredientRoute=raw.Name;binding.original.IngredientSO=raw;
            // Preserve existing, already tested species-specific fillet yields.
            binding.original.MinYieldRange=Mathf.Max(1,binding.original.MinYieldRange);binding.original.MaxYieldRange=Mathf.Max(binding.original.MinYieldRange+1,binding.original.MaxYieldRange);EditorUtility.SetDirty(binding.original);
        }
        foreach(var binding in config.species){
            bool collect=plan.collectionOnly.Any(c=>c.species==binding.definition.originalId);
            if(collect){binding.edible=false;binding.ingredientRoute="收藏 / 展示 / 出售";binding.original.IngredientSO=null;binding.original.MinYieldRange=binding.original.MaxYieldRange=0;}
            float nominal=Mathf.Max(.0001f,binding.definition.weight);
            int value=collect?Rank(binding.definition.rarity,45,90,180,450):Rank(binding.definition.rarity,plan.commonSale,plan.uncommonSale,plan.rareSale,plan.legendarySale);
            binding.original.DeadFishSalePrice=value/nominal;EditorUtility.SetDirty(binding.original);
            if(binding.shiny){binding.shiny.IngredientSO=null;binding.shiny.MinYieldRange=binding.shiny.MaxYieldRange=0;binding.shiny.DeadFishSalePrice=value*plan.shinyMultiplier/nominal;EditorUtility.SetDirty(binding.shiny);}
        }
        Localize(db);EditorUtility.SetDirty(db);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        PreserveLocaleEntries(localeBefore);
        // Refresh an editor-loaded database too; runtime starts with fresh caches.
        typeof(ItemDatabaseSO).GetMethod("RebuildLookups",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(db,null);
    }
    static void Localize(ItemDatabaseSO db){
        var tables=AssetDatabase.FindAssets("t:StringTable",new[]{"Assets/Settings/Localization/Tables/ItemsTable"}).Select(g=>AssetDatabase.LoadAssetAtPath<StringTable>(AssetDatabase.GUIDToAssetPath(g))).Where(t=>t).ToArray();
        // Register all appearances as well: the original ItemSO name lookup
        // returns an error string for a missing key instead of its fallback.
        foreach(var item in db._allItems.Where(i=>i&&i.Id.StartsWith("HTF_")))foreach(var table in tables){table.AddEntry("item."+item.Id+".name",item.Name);EditorUtility.SetDirty(table);EditorUtility.SetDirty(table.SharedData);}
        foreach(var guid in AssetDatabase.FindAssets("t:StringTable",new[]{"Assets/Settings/Localization/Tables/UiTable"})){
            var table=AssetDatabase.LoadAssetAtPath<StringTable>(AssetDatabase.GUIDToAssetPath(guid));if(!table)continue;
            table.AddEntry("fishing.collection_only","异色与收藏钓获物可收藏、展示或卖给商店老板，不能切片。");EditorUtility.SetDirty(table);EditorUtility.SetDirty(table.SharedData);
        }
    }
    static Dictionary<string,string> LocaleSnapshots()=>AssetDatabase.FindAssets("t:StringTable",new[]{"Assets/Settings/Localization/Tables/ItemsTable","Assets/Settings/Localization/Tables/UiTable"}).Select(AssetDatabase.GUIDToAssetPath).ToDictionary(p=>p,File.ReadAllText);
    sealed class LocaleBlock{public long id;public int start,end;}
    static LocaleBlock[] Blocks(string text){
        var matches=Regex.Matches(text,@"^  - m_Id: (\d+)\r?\n",RegexOptions.Multiline);var result=new List<LocaleBlock>();
        for(int i=0;i<matches.Count;i++){int start=matches[i].Index,end=i+1<matches.Count?matches[i+1].Index:text.Length;
            if(i+1==matches.Count){var footer=Regex.Match(text.Substring(start),@"^  [A-Za-z_]\w*:",RegexOptions.Multiline);if(footer.Success)end=start+footer.Index;}
            result.Add(new LocaleBlock{id=long.Parse(matches[i].Groups[1].Value),start=start,end=end});}return result.ToArray();
    }
    static void PreserveLocaleEntries(Dictionary<string,string> snapshots){
        // Unity's YAML rewrite can fold spacing inside unrelated long strings.
        // Retain their exact old entry blocks; update only our own name keys.
        foreach(var snapshot in snapshots){var table=Read<StringTable>(snapshot.Key);var old=Blocks(snapshot.Value).ToDictionary(b=>b.id);string current=File.ReadAllText(snapshot.Key);var output=new StringBuilder();int at=0;
            foreach(var block in Blocks(current)){output.Append(current,at,block.start-at);string key=table.SharedData.GetEntry(block.id)?.Key;bool owned=key!=null&&(key.StartsWith("item.HTF_",StringComparison.Ordinal)||key=="fishing.collection_only");
                if(!owned&&old.TryGetValue(block.id,out var original))output.Append(snapshot.Value,original.start,original.end-original.start);else output.Append(current,block.start,block.end-block.start);at=block.end;}
            output.Append(current,at,current.Length-at);string text=output.ToString();if(text!=current){File.WriteAllText(snapshot.Key,text,new UTF8Encoding(false));AssetDatabase.ImportAsset(snapshot.Key,ImportAssetOptions.ForceUpdate);}
        }
    }
    [MenuItem("Tools/Sushi and Fish/Verify Native Fish Cuisine")]
    public static void Verify(){
        Directory.CreateDirectory(Out);checks.Clear();Check(!EditorApplication.isPlaying,"Static data and recipe verification; no Play, player lifecycle or physics simulation");
        var plan=LoadPlan();var config=MigrationConfig.Load();var db=config.database;var resolver=new SushiRecipeResolver(db,null);var rows=new List<Row>();var sales=new List<Sale>();
        var keys=new HashSet<string>();
        Check(plan.entries.Length==53&&plan.collectionOnly.Length==7&&plan.entries.Select(e=>e.species).Distinct().Count()==53,"All 60 species classified: 53 normal-form recipes and 7 collection species");
        Check(plan.entries.All(e=>!plan.collectionOnly.Any(c=>c.species==e.species))&&config.species.All(b=>plan.entries.Any(e=>e.species==b.definition.originalId)||plan.collectionOnly.Any(c=>c.species==b.definition.originalId)),"No unclassified fish or cooking/collection overlap");
        Check(db._allItems.Where(i=>i).Select(i=>i.Id).Distinct().Count()==db._allItems.Count(i=>i),"All item identities unique, including existing game items");
        foreach(var entry in plan.entries){
            var binding=config.Find(entry.species);var source=Read<SushiSO>(entry.template);var rawSource=Read<IngredientSO>(entry.rawTemplate);var raw=Read<IngredientSO>(Root+"Ingredients/HTF_RAW_"+Number(entry.species)+".asset");var dish=Read<SushiSO>(Root+"Dishes/HTF_DISH_"+Number(entry.species)+".asset");
            Check(binding.edible&&binding.original.IngredientSO==raw&&raw.PlacablePrepStation&&raw.Prefab==rawSource.Prefab&&raw.PrepStationCuttingPrefab==rawSource.PrepStationCuttingPrefab&&raw.IngredientType==rawSource.IngredientType,entry.species+": caught fish uses original fillet machine and preparation station prefab/type");
            Check(db._rawIngredientItems.Contains(raw)&&db._allItems.Contains(raw)&&raw.ProcessedIngredients.All(p=>db._allItems.Contains(p)&&db._processedIngredientItems.Contains(p)),entry.species+": all raw and processed data registered for existing inventory, stock and save lookup");
            Check(raw.ProcessedIngredients.Count==rawSource.ProcessedIngredients.Count&&raw.ProcessedIngredients.Select((p,i)=>p.ProcessedType==rawSource.ProcessedIngredients[i].ProcessedType&&p.OutputYield==rawSource.ProcessedIngredients[i].OutputYield&&p.MaxPanCapacity==rawSource.ProcessedIngredients[i].MaxPanCapacity&&p.PanIngredientPrefab==rawSource.ProcessedIngredients[i].PanIngredientPrefab&&p.Sprite==rawSource.ProcessedIngredients[i].Sprite).All(ok=>ok),entry.species+": slice/strip method, capacity, yield, pan art and icons match native template");
            Check(dish.SushiType==source.SushiType&&dish.Recipe.Ingredients.Select(n=>n.Amount).SequenceEqual(source.Recipe.Ingredients.Select(n=>n.Amount))&&dish.Recipe.ProcessedIngredients.Select(n=>n.Amount).SequenceEqual(source.Recipe.ProcessedIngredients.Select(n=>n.Amount)),entry.species+": complete original recipe operation order and amounts retained");
            Check(dish.Prefab==source.Prefab&&dish.GraphicObjectPrefab==source.GraphicObjectPrefab&&dish.PlacementObjectPrefab==source.PlacementObjectPrefab&&dish.SingleSushiPrefab==source.SingleSushiPrefab&&dish.Sprite==source.Sprite,entry.species+": native plated/single-piece/conveyor graphic and original painted menu icon reused");
            Check(db._servedDishItems.Contains(dish)&&db._allItems.Contains(dish)&&resolver.TryResolveById(dish.Id,out var resolved)&&resolved.BuiltInSushi==dish,entry.species+": native menu, chef recipe book and customer order resolver see this dish");
            Check(SushiCompositionData.TryCreateFromBuiltIn(dish,out var composition)&&resolver.ResolveExact(composition)?.RecipeId==dish.Id,entry.species+": own fish ingredient identifies the exact finished dish, rather than another species");
            string signature=dish.SushiType+":"+string.Join(",",composition.IngredientIds);Check(keys.Add(signature),entry.species+": unique native recipe composition");
            Check(dish.Recipe.ProcessedIngredients.Where(n=>n.Data.Id.StartsWith("HTF_CUT_")).Any()&&dish.Recipe.ProcessedIngredients.Where(n=>n.Data.Id.StartsWith("HTF_CUT_")).All(n=>raw.ProcessedIngredients.Contains(n.Data)),entry.species+": required seafood cuts actually come from this caught fish");
            Check(dish.Recipe.Ingredients.All(n=>n.Data==raw||!n.Data.Id.StartsWith("HTF_"))&&dish.Recipe.ProcessedIngredients.All(n=>raw.ProcessedIngredients.Contains(n.Data)||!n.Data.Id.StartsWith("HTF_")),entry.species+": remaining supplies are existing rice, nori and vegetables");
            Check(raw.Sprite==rawSource.Sprite&&raw.GraphicObjectPrefab==rawSource.GraphicObjectPrefab&&dish.Price>=source.Price,entry.species+": existing raw-ingredient art and sensible native dish pricing retained");
            rows.Add(new Row{species=entry.species,fish=binding.definition.displayName,dish=dish.Name,id=dish.Id,price=dish.Price,kind=dish.SushiType.ToString(),template=entry.template,raw=raw.Id,ingredients=dish.Recipe.Ingredients.Select(n=>n.Data.Name+" ×"+n.Amount).Concat(dish.Recipe.ProcessedIngredients.Select(n=>n.Data.Name+" ×"+n.Amount)).ToArray()});
        }
        foreach(var binding in config.species){
            bool collection=plan.collectionOnly.Any(c=>c.species==binding.definition.originalId);
            if(collection)Check(!binding.edible&&!binding.original.IngredientSO&&binding.original.MinYieldRange==0&&binding.original.MaxYieldRange==0,binding.definition.originalId+": unusual catch stays whole for the original merchant/trophy flow");
            if(binding.shiny)Check(!binding.shiny.IngredientSO&&binding.shiny.MinYieldRange==0&&binding.shiny.MaxYieldRange==0,binding.definition.originalId+": alternate appearance cannot enter cooking and remains saleable");
            foreach(var fish in new[]{binding.original,binding.shiny}.Where(f=>f)){
                float weight=Mathf.Max(.0001f,binding.definition.weight);Check(float.IsFinite(fish.DeadFishSalePrice)&&fish.DeadFishSalePrice>0&&Mathf.RoundToInt(fish.MinWeight*fish.DeadFishSalePrice)>0,fish.Id+": existing per-kilogram merchant calculation pays even for the smallest specimen");
                sales.Add(new Sale{id=fish.Id,name=fish.Name,pricePerKg=fish.DeadFishSalePrice,nominalWeight=weight,nominalPrice=Mathf.RoundToInt(weight*fish.DeadFishSalePrice),collectionOnly=collection||fish==binding.shiny});
            }
        }
        var buyer=Read<GameObject>("Assets/GameObject/Resident_Gabe.prefab").GetComponent<ResidentDeadFishBuyer>();Check(buyer&&buyer.CanBuyDeadFish,"Original shop owner Gabe already buys held dead fish through native interaction/RPC/payment/UI");
        string buyerSource=File.ReadAllText("Assets/Scripts/Assembly-CSharp/PaperPixelGames/Core/Ai/NPCs/ResidentDeadFishBuyer.cs");Check(buyerSource.Contains("deadFish.Weight.Value")&&buyerSource.Contains("deadFish.FishSO.DeadFishSalePrice")&&buyerSource.Contains("service.EarnMoney(num")&&buyerSource.Contains("deadFish.NetworkObject.Despawn()"),"Original sale method pays currency and consumes exactly the sold fish without custom processing code");
        var tables=AssetDatabase.FindAssets("t:StringTable",new[]{"Assets/Settings/Localization/Tables/ItemsTable"}).Select(g=>AssetDatabase.LoadAssetAtPath<StringTable>(AssetDatabase.GUIDToAssetPath(g))).Where(t=>t).ToArray();
        foreach(var table in tables)Check(db._allItems.Where(i=>i&&i.Id.StartsWith("HTF_")).All(i=>table.GetEntry("item."+i.Id+".name")?.LocalizedValue==i.Name),table.LocaleIdentifier.Code+": native name lookup contains all fish, meat, cuts and dish names");
        foreach(var guid in AssetDatabase.FindAssets("t:StringTable",new[]{"Assets/Settings/Localization/Tables/UiTable"})){
            var table=AssetDatabase.LoadAssetAtPath<StringTable>(AssetDatabase.GUIDToAssetPath(guid));if(table)Check(table.GetEntry("fishing.collection_only")?.LocalizedValue=="异色与收藏钓获物可收藏、展示或卖给商店老板，不能切片。",table.LocaleIdentifier.Code+": existing fillet-machine collection warning has a complete native localization key");
        }
        var report=new Report{dishes=rows.Count,raw=rows.Count,processed=db._processedIngredientItems.Count(p=>p&&p.Id.StartsWith("HTF_CUT_")),originalDishes=db._servedDishItems.OfType<SushiSO>().Count(i=>!i.Id.StartsWith("HTF_")),totalDishes=db._servedDishItems.OfType<SushiSO>().Count(),entries=rows.ToArray(),sales=sales.ToArray()};
        File.WriteAllText(Out+"CuisineReport.json",JsonUtility.ToJson(report,true));File.WriteAllText(Root+"CuisineGuide.md",Guide(report,plan));AssetDatabase.Refresh();
        InstallFishingMigrationAudit.Verify();Debug.Log("FISHING_CUISINE_VERIFIED checks="+checks.Count+" dishes="+report.dishes+" appearances="+sales.Count+" play=false simulation=false nativeProcessingUnchanged=true");
    }
    static string Guide(Report report,Plan plan){
        var text=new System.Text.StringBuilder("# 海钓菜谱与出售说明\n\n53道新增菜保留原来的42道寿司，合计95道。正常外形的鱼均按鱼种分配一道菜（不限稀有度）。下表对应原菜单可选菜谱、厨台配方和顾客点单身份。\n\n制作：捕获后将普通外形的原色鱼送入现有切鱼机，取得该鱼的净肉；放到原备料台，按原来的切片/切条方式加工并放入料盘；在原厨台配方书选择菜名，按配方摆放原有米饭、海苔和配菜，再按既有握制、卷制、切段或装盘方式完成。放到原传送带/餐桌的步骤保持。未制作的菜按原菜单规则显示库存状态，在平板菜单内用原按键上架。\n\n净肉与加工品只复制原生食材数据，保留原物件、贴图、切法、产量和料盘。没有新增工具、食材处理类型、交互按键、厨房代码或UI界面。\n\n|鱼种|菜名|售价|配料（份数沿用原菜谱）|\n|---|---|---:|---|\n");
        foreach(var row in report.entries)text.AppendLine("|"+row.fish+"|"+row.dish+"|"+row.price+"|"+string.Join("、",row.ingredients)+"|");
        text.AppendLine("\n## 异色与奇怪钓获物\n\n异色整鱼和以下收藏类整鱼不提供切鱼原料，可从鱼仓取出拿在手里，走到原商店老板Gabe面前，沿用原提示按鼠标左键出售；按实际重量×单价付款，并移除这一条鱼。图鉴中的已获得/异色记录保留。\n");
        foreach(var row in plan.collectionOnly)text.AppendLine("- "+row.species+"："+row.reason);
        text.AppendLine("\n|钓获物|标准体重售价|状态|\n|---|---:|---|");foreach(var sale in report.sales.Where(s=>s.collectionOnly))text.AppendLine("|"+sale.name+"|"+sale.nominalPrice+"|收藏、展示、出售|");
        text.AppendLine("\n原菜单卡片、按钮、边框、字体、详情页、配方书和交易提示均保留原资产；新菜直接引用对应原寿司的图标和模型。旧存档里的整鱼无需转换，继续沿用原ID即可出新净肉。更新前已经切好的旧通用鱼肉仍可做原有菜谱，不会被替换或删除。\n\n验证使用Unity编译、静态数据检查和原生纯配方解析，未进入Play或模拟游戏。\n");return text.ToString();
    }
}
