using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Unity.Netcode;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.DressingSceneSystem;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.WorldUISystem;
using SushiAndFish.CityExpansion;
using Object=UnityEngine.Object;

public static partial class CityExpansionInstallation
{
    [Serializable]public class RestoreRow {public string id,scene,path,prefab,restoreReason;public Vector3 position,scale,min,max;public Quaternion rotation;}
    [Serializable]public class RestorePlan {public RestoreRow[] objects;}
    [Serializable]public class RoutePoint {public float x,z;public Vector3 Vector=>new Vector3(x,0,z);}
    [Serializable]public class Route {public string id;public RoutePoint a,b;}
    [Serializable]public class RoutePlan {public Route[] roads;}
    [Serializable]public class RestoreResult {public string sourcePath,prefab;public Vector3 min,max;public float boundsError;}
    [Serializable]public class BuildingResult {public string name,plot;public Vector3 origin,min,max,door,street;public float yaw,distanceToCasino,distanceToShops;}
    [Serializable]public class SeatResult {public int id,table;public bool bar,completePath;public Vector3 seat,approach;public float mountDistance;public uint networkHash;}
    [Serializable]public class Conflict {public string road,collider;public Vector3 point;}
    [Serializable]public class WorldReceipt
    {
        public string utc,stage,error,navigation;public bool saved;public int population,roadSamples,missingScripts;
        public List<RestoreResult> restored=new List<RestoreResult>();public List<BuildingResult> buildings=new List<BuildingResult>();public List<SeatResult> seats=new List<SeatResult>();public List<Conflict> roadConflicts=new List<Conflict>();
        public List<string> diagnostics=new List<string>();public int[] probabilityCounts;
    }
    static WorldReceipt world;
    static Scene gameScene;
    static Transform cityBuildings;
    static WorldUIAnchor nativeSeatAnchor;
    static string WorldPath=>Path.Combine(CityExpansionInspection.Reports,"city-expansion-world-build.json");
    static IEnumerable<Transform> All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
    static Transform NewRoot(string name,Scene scene){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go.transform;}
    static Transform NewChild(Transform parent,string name,Vector3 worldPosition,Quaternion rotation)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(worldPosition,rotation);return go.transform;}
    static void Property(Object obj,string field,object value)
    {
        var data=new SerializedObject(obj);var p=data.FindProperty(field);if(p==null)throw new Exception("Missing serialized field "+obj.GetType().Name+"."+field);
        if(value is Object reference)p.objectReferenceValue=reference;else if(value is Vector3 vector)p.vector3Value=vector;else if(value is bool flag)p.boolValue=flag;else if(value is float number)p.floatValue=number;else if(value is int index)p.intValue=index;else throw new Exception("Unsupported property value.");
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static float Ground(float x,float z)
    {
        float top=float.NegativeInfinity;
        foreach(var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            var p=terrain.transform.position;var size=terrain.terrainData.size;
            if(x>=p.x&&x<=p.x+size.x&&z>=p.z&&z<=p.z+size.z)top=Mathf.Max(top,p.y+terrain.SampleHeight(new Vector3(x,0,z)));
        }
        if(float.IsNegativeInfinity(top))throw new Exception("No terrain under the new building.");
        return top;
    }
    public static void BuildWorld()
    {
        world=new WorldReceipt{utc=DateTime.UtcNow.ToString("o"),stage="START"};
        try
        {
            if(!File.Exists(Path.Combine(CityExpansionInspection.Reports,"city-expansion-backup.json")))throw new Exception("Backup missing.");
            gameScene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
            var scenes=new List<Scene>{gameScene};
            foreach(var name in new[]{"100_Dressing_City_01","110_Dressing_City_Folliage_01","120_Dressing_City_Grass_01","130_Dressing_City_Rocks_01"})
                scenes.Add(EditorSceneManager.OpenScene(IslandFrontEndBuilder.DressingRoot+"/"+name+".unity",OpenSceneMode.Additive));
            foreach(var s in scenes)foreach(var old in s.GetRootGameObjects().Where(g=>g.name=="CityBuildings_20261021"||g.name=="RestoredIslandPerimeter_20261021"))Object.DestroyImmediate(old);
            var nativeArea=All(gameScene).Select(t=>t.GetComponent<EmoteArea>()).First(a=>a&&new SerializedObject(a).FindProperty("_emote").enumValueIndex==0);
            nativeSeatAnchor=nativeArea.WorldUIAnchor;
            cityBuildings=NewRoot("CityBuildings_20261021",gameScene);
            var restaurant=PlaceBuilding("AR_Restaurant","P08",new Vector3(72,Ground(72,-13)+.045f,-13),90,new Vector3(64.6f,3.045f,-13));
            var cinema=PlaceBuilding("AR_CinemaLobby","P13",new Vector3(88,Ground(88,-52.5f)+.045f,-52.5f),270,new Vector3(95.4f,3.045f,-52.5f));
            var auditorium=PlaceBuilding("AR_CinemaAuditorium","Independent auditorium",new Vector3(1000,200,-1000),0,Vector3.zero);
            var ignore=auditorium.gameObject.AddComponent<NavMeshModifier>();ignore.ignoreFromBuild=true;
            foreach(var renderer in auditorium.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.name.Contains("Seat_R")&&(renderer.name.EndsWith("_Cushion")||renderer.name.EndsWith("_BackShell"))||Regex.IsMatch(renderer.name,@"Tier_|HalfStep|RearCorridor"))
                {var c=renderer.gameObject.AddComponent<MeshCollider>();c.sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;}
            AddDoor(restaurant,"AR_Restaurant_Entrance");
            AddDoor(cinema,"AR_CinemaLobby_MainEntrance");
            AddInteriorLights(restaurant,3.05f);AddInteriorLights(cinema,3.6f);AddInteriorLights(auditorium,4.4f);
            AddConnector(restaurant,"RestaurantStreetPaving",-4.58f,64.6f,3.6f);
            AddConnector(cinema,"CinemaStreetPaving",-6.4f,95.4f,3.6f);
            HidePlotReservation("P08");HidePlotReservation("P13");
            var seats=AddSeats(restaurant);
            var meals=restaurant.gameObject.AddComponent<TownMealScheduler>();meals.seats=seats;
            meals.sushiRestaurant=All(gameScene).Select(t=>t.GetComponent<RestaurantManager>()).First(r=>r);
            AddPortals(cinema,auditorium);
            RestorePerimeter(scenes);
            ApplyCoastalRoadGeometry();
            Physics.SyncTransforms();
            ProbeRoads();
            if(world.roadConflicts.Count>0)throw new Exception("Restored perimeter intersects current road clearance. Read roadConflicts before changing the road plan.");
            BakeAndResolveSeats(scenes,seats);
            var manager=All(gameScene).Select(t=>t.GetComponent<ResidentManager>()).First(m=>m);
            world.population=CityExpansionInspection.Field<List<ResidentManager.HouseBinding>>(manager,"_houseBindings").Where(h=>h.HouseData).SelectMany(h=>h.HouseData.Residents).Where(r=>r.ResidentData).Select(r=>r.ResidentData.NpcID).Distinct().Count();
            var counts=new int[4];for(int day=1;day<=10000;day++)for(int window=0;window<2;window++)if(TownMealScheduler.PlannedEvent(day,window,out int size,out _)){counts[0]++;counts[size]++;}world.probabilityCounts=counts;
            world.missingScripts=scenes.SelectMany(All).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if(world.missingScripts!=0)throw new Exception("Missing scripts in the city scene.");
            foreach(var scene in scenes)EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            world.saved=true;world.stage="SAVED_NEEDS_RUNTIME_AND_VISUAL_VERIFICATION";
            File.WriteAllText(Path.Combine(CityExpansionInspection.Reports,"town-road-layout-plan.json"),File.ReadAllText(Path.Combine(CityExpansionInspection.Reports,"city-coastal-road-layout-plan.json")));
            File.WriteAllText(WorldPath,JsonUtility.ToJson(world,true));
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            Debug.Log("CITY_EXPANSION_WORLD_SAVED perimeter="+world.restored.Count+" seats="+world.seats.Count+" population="+world.population);
        }
        catch(Exception e){world.error=e.ToString();world.stage="FAILED";throw;}
        finally{File.WriteAllText(WorldPath,JsonUtility.ToJson(world,true));}
    }
    static Transform PlaceBuilding(string asset,string plot,Vector3 point,float yaw,Vector3 street)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(CityRoot+"/Prefabs/"+asset+".prefab");if(!prefab)throw new Exception("Imported building missing: "+asset);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,gameScene);go.name=asset;go.transform.SetParent(cityBuildings,true);go.transform.SetPositionAndRotation(point,Quaternion.Euler(0,yaw,0));
        PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        var b=IslandComprehensiveRepair.BoundsOf(go.transform);
        world.buildings.Add(new BuildingResult{name=asset,plot=plot,origin=point,yaw=yaw,min=b.min,max=b.max,door=go.transform.TransformPoint(new Vector3(0,0,asset=="AR_Restaurant"?-4.015f:-5.015f)),street=street,distanceToCasino=Vector2.Distance(new Vector2(point.x,point.z),new Vector2(-101,-35)),distanceToShops=Vector2.Distance(new Vector2(point.x,point.z),new Vector2(-15,-10))});
        return go.transform;
    }
    static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    static void AddDoor(Transform root,string prefix)
    {
        var left=Find(root,prefix+"_L");var right=Find(root,prefix+"_R");
        var center=(left.position+right.position)*.5f;center.y=root.position.y;
        var door=root.gameObject.AddComponent<CityAutomaticDoor>();door.leftLeaf=left;door.rightLeaf=right;door.doorway=NewChild(root,"AutomaticDoorSensor",center,root.rotation);
        door.leftClosed=left.localPosition;door.rightClosed=right.localPosition;
        door.leftOpen=left.localPosition+left.parent.InverseTransformVector(-root.right*1.28f);
        door.rightOpen=right.localPosition+right.parent.InverseTransformVector(root.right*1.28f);
        foreach(var leaf in new[]{left,right}){var modifier=leaf.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;}
    }
    static void AddInteriorLights(Transform root,float height)
    {
        foreach(float x in new[]{-2.3f,2.3f})foreach(float z in new[]{-2.2f,2.2f})
        {
            var t=NewChild(root,"InteriorWarmLight",root.TransformPoint(new Vector3(x,height,z)),Quaternion.identity);
            var light=t.gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=7;light.intensity=1.2f;light.color=new Color(1,.88f,.71f);light.shadows=LightShadows.None;
        }
    }
    static Material paving;
    static void AddConnector(Transform building,string name,float localFront,float streetX,float width)
    {
        if(!paving){paving=AssetDatabase.LoadAssetAtPath<Material>(CityRoot+"/Materials/PublicEntrancePaving.mat");if(!paving){paving=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="PublicEntrancePaving"};paving.SetColor("_BaseColor",new Color(.52f,.48f,.40f));paving.SetFloat("_Smoothness",.16f);AssetDatabase.CreateAsset(paving,CityRoot+"/Materials/PublicEntrancePaving.mat");}}
        Vector3 apron=building.TransformPoint(new Vector3(0,0,localFront));float length=Mathf.Abs(streetX-apron.x)+.02f;
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(cityBuildings,false);go.transform.position=new Vector3((streetX+apron.x)*.5f,building.position.y-.025f,building.position.z);go.transform.localScale=new Vector3(length,.05f,width);go.GetComponent<Renderer>().sharedMaterial=paving;
        // Dark insets give this pedestrian connection a paving pattern distinct
        // from the carriageway, while keeping a continuous physical surface.
        var groutMat=AssetDatabase.LoadAssetAtPath<Material>(CityRoot+"/Materials/PublicEntranceGrout.mat");if(!groutMat){groutMat=new Material(paving){name="PublicEntranceGrout"};groutMat.SetColor("_BaseColor",new Color(.19f,.19f,.17f));AssetDatabase.CreateAsset(groutMat,CityRoot+"/Materials/PublicEntranceGrout.mat");}
        int rows=Mathf.CeilToInt(length/.55f);for(int i=1;i<rows;i++)
        {var stripe=GameObject.CreatePrimitive(PrimitiveType.Cube);stripe.name=name+"_Grout";stripe.transform.SetParent(cityBuildings,false);stripe.transform.position=new Vector3(Mathf.Min(streetX,apron.x)+i*length/rows,building.position.y+.001f,building.position.z);stripe.transform.localScale=new Vector3(.018f,.002f,width);stripe.GetComponent<Renderer>().sharedMaterial=groutMat;Object.DestroyImmediate(stripe.GetComponent<Collider>());}
    }
    static void HidePlotReservation(string id)
    {
        var plot=All(gameScene).FirstOrDefault(t=>t.name.StartsWith(id+"_Reserved_"));if(!plot)throw new Exception("Expected reserved plot missing: "+id);
        foreach(var renderer in plot.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter=renderer.GetComponent<MeshFilter>();UnityEngine.Object.DestroyImmediate(renderer);
            if(filter)UnityEngine.Object.DestroyImmediate(filter);
        }
        plot.gameObject.SetActive(false);
    }
    static CityDiningSeat[] AddSeats(Transform restaurant)
    {
        var parent=NewChild(restaurant,"NativeInteractiveSeating",restaurant.position,restaurant.rotation);var result=new List<CityDiningSeat>();
        var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/EmoteArea.prefab");
        var meshes=restaurant.GetComponentsInChildren<MeshRenderer>(true).Where(r=>Regex.IsMatch(r.name,@"Chair.*_Seat$|StoolSeat")).OrderBy(r=>r.name).ToArray();
        foreach(var mesh in meshes)
        {
            bool bar=mesh.name.Contains("StoolSeat");Vector3 center=mesh.bounds.center;Vector3 local=restaurant.InverseTransformPoint(center);Vector3 facing;
            if(bar)facing=restaurant.forward;
            else{var back=Find(restaurant,mesh.name.Replace("_Seat","_Back")).GetComponent<Renderer>();facing=Vector3.ProjectOnPlane(center-back.bounds.center,Vector3.up).normalized;}
            Vector3 pose=new Vector3(center.x,restaurant.position.y+(bar?.25f:0),center.z);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(template,gameScene);go.name="DiningSeat_"+result.Count.ToString("00");go.transform.SetParent(parent,true);go.transform.SetPositionAndRotation(pose,Quaternion.LookRotation(facing,Vector3.up));go.transform.localScale=Vector3.one;
            PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var seat=go.AddComponent<CityDiningSeat>();seat.seatId=result.Count;seat.barStool=bar;seat.tableId=bar?4:(local.x<0?0:2)+(local.z<-1.8f?0:1);
            seat.seatedPose=NewChild(go.transform,"SeatedPose",pose,go.transform.rotation);
            var approach=restaurant.TransformPoint(new Vector3(Mathf.Clamp(local.x,-1.65f,1.65f),0,bar?-.5f:local.z));
            seat.approach=NewChild(go.transform,"WalkApproach",approach,go.transform.rotation);
            var area=go.GetComponent<EmoteArea>();Property(area,"_emote",0);Property(area,"_targetPos",pose);Property(area,"_targetRot",go.transform.eulerAngles);Property(area,"_allowOpenEmotesInput",false);
            var anchor=go.GetComponent<WorldUIAnchor>();var anchorPoint=go.transform.Find("AnchorPoint");EditorUtility.CopySerialized(nativeSeatAnchor,anchor);Property(anchor,"_anchorPoint",anchorPoint);Property(anchor,"_worldOffset",Vector3.up*.8f);Property(anchor,"_maxDistance",3.5f);
            Property(area,"_worldUIAnchor",anchor);
            var sphere=go.GetComponent<SphereCollider>();sphere.radius=.38f;sphere.center=Vector3.up*.47f;sphere.isTrigger=true;
            result.Add(seat);
        }
        if(result.Count!=12)throw new Exception("All 12 restaurant seats must be wired, got "+result.Count);
        return result.ToArray();
    }
    static void AddPortals(Transform lobby,Transform auditorium)
    {
        var arrival=Find(auditorium,"AR_CinemaAuditorium_Auditorium_Arrival");
        var toRoom=NewChild(cityBuildings,"AuditoriumEntryArrival",arrival.position+Vector3.up*.04f,Quaternion.Euler(0,180,0));
        var lobbyArrival=Find(lobby,"AR_CinemaLobby_AuditoriumArrivalReturn");
        var toLobby=NewChild(cityBuildings,"CinemaLobbyReturnArrival",lobbyArrival.position+Vector3.up*.04f,lobby.rotation*Quaternion.Euler(0,180,0));
        Portal(lobby,Find(lobby,"AR_CinemaLobby_AuditoriumPortal").position,toRoom,"EnterAuditorium","进入放映厅");
        Portal(auditorium,Find(auditorium,"AR_CinemaAuditorium_ReturnEntryClosedLeaf").GetComponent<Renderer>().bounds.center-auditorium.forward*.05f,toLobby,"ReturnToCinemaLobby","返回影院大厅");
    }
    static void Portal(Transform owner,Vector3 point,Transform destination,string name,string prompt)
    {
        var t=NewChild(owner,name,point,owner.rotation);t.gameObject.layer=LayerMask.NameToLayer("Interactable");
        var collider=t.gameObject.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=new Vector3(1.7f,2.3f,.35f);
        var portal=t.gameObject.AddComponent<CityCinemaPortal>();portal.destination=destination;portal.prompt=prompt;
        var anchor=t.gameObject.AddComponent<WorldUIAnchor>();Property(anchor,"_anchorPoint",t);Property(anchor,"_isVisible",false);portal.anchor=anchor;
    }
    static void RestorePerimeter(List<Scene> scenes)
    {
        var plan=JsonUtility.FromJson<RestorePlan>(File.ReadAllText(Path.Combine(CityExpansionInspection.Reports,"city-perimeter-restore-plan.json")));
        var roots=new Dictionary<string,Transform>();
        foreach(var row in plan.objects)
        {
            if(!roots.TryGetValue(row.scene,out var root)){root=NewRoot("RestoredIslandPerimeter_20261021",scenes.Single(s=>s.path==row.scene));roots.Add(row.scene,root);}
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(row.prefab);if(!prefab)throw new Exception("Archived perimeter prefab missing: "+row.prefab);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.gameObject.scene);go.name=row.path.Split('/').Last();go.transform.SetParent(root,true);go.transform.SetPositionAndRotation(row.position,row.rotation);go.transform.localScale=row.scale;
            var bounds=IslandComprehensiveRepair.BoundsOf(go.transform);float difference=Mathf.Max(Vector3.Distance(bounds.min,row.min),Vector3.Distance(bounds.max,row.max));
            world.restored.Add(new RestoreResult{sourcePath=row.path,prefab=row.prefab,min=bounds.min,max=bounds.max,boundsError=difference});
            if(difference>.035f)throw new Exception("Restored source geometry differs from the archived world bounds: "+row.path+" difference="+difference);
        }
    }
    static void ProbeRoads()
    {
        var plan=JsonUtility.FromJson<RoutePlan>(File.ReadAllText(Path.Combine(CityExpansionInspection.Reports,"city-coastal-road-layout-plan.json")));
        foreach(var road in plan.roads)
        {
            var a=road.a.Vector;var b=road.b.Vector;var forward=(b-a).normalized;var side=new Vector3(-forward.z,0,forward.x);int count=Mathf.CeilToInt(Vector3.Distance(a,b));
            for(int i=0;i<=count;i++)foreach(float lateral in new[]{-5.4f,-2f,0f,2f,5.4f})
            {
                var p=Vector3.Lerp(a,b,i/(float)count)+side*lateral;p.y=3.06f;world.roadSamples++;
                foreach(var hit in Physics.OverlapCapsule(p+Vector3.up*.32f,p+Vector3.up*1.42f,.28f,~0,QueryTriggerInteraction.Ignore))
                {
                    var top=hit.transform.root;
                    if(top.name=="RestoredIslandPerimeter_20261021")world.roadConflicts.Add(new Conflict{road=road.id,point=p,collider=CityExpansionInspection.PathOf(hit.transform)});
                }
            }
        }
        // Include the two rounded coastal ends, beyond their centre lines.
        for(float x=78;x<=111;x+=.5f)for(float z=-75;z<=-49;z+=.5f)
        {
            var ground=Physics.RaycastAll(new Vector3(x,4,z),Vector3.down,1.2f,~0,QueryTriggerInteraction.Ignore).FirstOrDefault(h=>h.collider.name=="Unified_Carriageway"||h.collider.name=="Unified_Sidewalks");
            if(!ground.collider)continue;
            var p=ground.point;world.roadSamples++;
            foreach(var hit in Physics.OverlapCapsule(p+Vector3.up*.32f,p+Vector3.up*1.42f,.28f,~0,QueryTriggerInteraction.Ignore))
                if(hit.transform.root.name=="RestoredIslandPerimeter_20261021")world.roadConflicts.Add(new Conflict{road="CoastalEndSurface",point=p,collider=CityExpansionInspection.PathOf(hit.transform)});
        }
    }
    static void BakeAndResolveSeats(List<Scene> scenes,CityDiningSeat[] seats)
    {
        var nodes=scenes.SelectMany(All).ToArray();var activation=nodes.ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);
        foreach(var root in nodes.Select(t=>t.GetComponent<DressingSceneRoot>()).Where(r=>r))root.ActivateAllImmediate();
        var surface=nodes.Select(t=>t.GetComponent<NavMeshSurface>()).First(s=>s&&CityExpansionInspection.PathOf(s.transform)=="---AI/ResidentManager/NavMesh Surface");
        surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
        string path=AssetDatabase.GenerateUniqueAssetPath(CityRoot+"/TownNavigation_20261021.asset");AssetDatabase.CreateAsset(surface.navMeshData,path);EditorUtility.SetDirty(surface);world.navigation=path;
        var filter=new NavMeshQueryFilter{agentTypeID=surface.agentTypeID,areaMask=NavMesh.AllAreas};
        foreach(var seat in seats)
        {
            Vector3 origin=seat.approach.position;if(!NavMesh.SamplePosition(origin,out var hit,.9f,filter))throw new Exception("No navigation approach to seat "+seat.seatId);
            seat.approach.position=hit.position;
            var navPath=new NavMeshPath();bool complete=NavMesh.CalculatePath(new Vector3(66,3.1f,-13),hit.position,filter,navPath)&&navPath.status==NavMeshPathStatus.PathComplete;
            var net=seat.GetComponent<NetworkObject>();
            world.seats.Add(new SeatResult{id=seat.seatId,table=seat.tableId,bar=seat.barStool,seat=seat.seatedPose.position,approach=hit.position,completePath=complete,mountDistance=Vector3.Distance(seat.seatedPose.position,hit.position),networkHash=(uint)new SerializedObject(net).FindProperty("GlobalObjectIdHash").longValue});
            if(!complete)throw new Exception("Incomplete street-to-restaurant seating path "+seat.seatId);
        }
        foreach(var pair in activation)if(pair.Key&&pair.Key.activeSelf!=pair.Value)pair.Key.SetActive(pair.Value);
    }
}
