using System;using System.Linq;using System.Reflection;using UnityEngine;using PaperPixelGames.Core.Tablet;using SushiAndFish.ResidentialIsland;using Michsky.MUIP;
namespace SushiAndFish.FriendsLife
{
    [Serializable]public sealed class FriendMapViewState
    {
        public int layer,floor;public Vector2 center;public float zoom,fit;
        public bool cardOpen,homeVisit;public string buildingId,houseInstanceId,visitorId,worldId;
    }
    public static class FriendMapVisitView
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly FieldInfo Zoomer=typeof(MapApp).GetField("_mapZoomer",Flags),Dropdown=typeof(MapApp).GetField("_mapDropdown",Flags),Content=typeof(MapZoomer).GetField("_content",Flags),Minimum=typeof(MapZoomer).GetField("_minScale",Flags),Maximum=typeof(MapZoomer).GetField("_maxScale",Flags),Speed=typeof(MapZoomer).GetField("_zoomSpeed",Flags);
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static Vector2 WorldToUv(Rect bounds,Vector3 world)
        {
            if(!Finite(bounds.xMin)||!Finite(bounds.yMin)||!Finite(bounds.width)||!Finite(bounds.height)||bounds.width<=0||bounds.height<=0||!Finite(world.x)||!Finite(world.z))throw new InvalidOperationException("Actual live map world bounds unavailable");return new Vector2((world.x-bounds.xMin)/bounds.width,(world.z-bounds.yMin)/bounds.height);
        }
        public static Vector2 CenterFromContent(Vector2 position,Vector2 size,float zoom)
        {if(!Finite(zoom)||zoom<=0||!Finite(size.x)||!Finite(size.y)||size.x<=0||size.y<=0||!Finite(position.x)||!Finite(position.y))throw new InvalidOperationException("Actual map view geometry unavailable");return new Vector2(.5f-position.x/(size.x*zoom),.5f-position.y/(size.y*zoom));}
        public static float RestoredZoom(float saved,float oldFit,float currentFit,float maximum)
        {if(!Finite(saved)||!Finite(oldFit)||saved<=0||oldFit<=0||!Finite(currentFit)||!Finite(maximum)||currentFit<=0||maximum<currentFit)throw new InvalidOperationException("Actual map zoom bounds unavailable");return Mathf.Clamp(currentFit*(saved/oldFit),currentFit,maximum);}
        static MapZoomer Resolve(TabletController owner,out MapApp app,out LiveSceneTabletMap live,out RectTransform content,out MapZoomSettings settings)
        {
            app=owner?owner.MapApplication:null;live=app?app.GetComponent<LiveSceneTabletMap>():null;
            if(!app||!live||!live.MapCamera||Zoomer==null||Dropdown==null||Content==null||Minimum==null||Maximum==null||Speed==null)throw new InvalidOperationException("Original live map/zoom binding unavailable");
            var zoomer=Zoomer.GetValue(app)as MapZoomer;content=zoomer?Content.GetValue(zoomer)as RectTransform:null;
            if(!zoomer||!content)throw new InvalidOperationException("Original map content unavailable");Canvas.ForceUpdateCanvases();settings=new MapZoomSettings((float)Minimum.GetValue(zoomer),content.localScale.x,(float)Maximum.GetValue(zoomer),(float)Speed.GetValue(zoomer));return zoomer;
        }
        static void Layer(MapApp app,LiveSceneTabletMap live,int layer)
        {
            if(layer<0||layer>1)throw new InvalidOperationException("Unknown original map layer");if(live.IsUnderwater==(layer==1))return;
            var dropdown=Dropdown.GetValue(app)as CustomDropdown;if(!dropdown||dropdown.items.Count<=layer)throw new InvalidOperationException("Original map layer dropdown unavailable");
            // Same original pair as an authored dropdown item. The original
            // MapApp owns SetLayer/projection/camera; no camera setter here.
            dropdown.SetDropdownIndex(layer);dropdown.onValueChanged.Invoke(layer);
        }
        public static HousingMap PrepareSurface(TabletController owner)
        {
            MapApp app;LiveSceneTabletMap live;RectTransform content;MapZoomSettings settings;Resolve(owner,out app,out live,out content,out settings);Layer(app,live,0);
            var map=app.GetComponent<HousingMap>();if(!map)throw new InvalidOperationException("Original housing map unavailable");
            if(!map.Overlay){var configure=typeof(HousingMap).GetMethod("Configure",Flags);if(configure==null)throw new InvalidOperationException("Original housing map UI binding unavailable");configure.Invoke(map,null);}
            if(!map.Overlay||!map.IsVisible)throw new InvalidOperationException("Original housing map is not visible");return map;
        }
        public static void Focus(TabletController owner,Vector3 actualHome)
        {
            PrepareSurface(owner);MapApp app;LiveSceneTabletMap live;RectTransform content;MapZoomSettings settings;var zoomer=Resolve(owner,out app,out live,out content,out settings);var uv=WorldToUv(live.WorldBounds,actualHome);
            if(uv.x<0||uv.x>1||uv.y<0||uv.y>1)throw new InvalidOperationException("Actual home is outside the live surface map");
            // At overview fit the ScrollRect correctly clamps the entire map
            // to its midpoint. Zoom enough for this real location to occupy
            // the viewport center, within the original zoom limits.
            var viewport=content.parent as RectTransform;if(!viewport)throw new InvalidOperationException("Original map viewport unavailable");
            float horizontal=viewport.rect.width/(content.rect.width*Mathf.Max(.0001f,2*Mathf.Min(uv.x,1-uv.x)));
            float vertical=viewport.rect.height/(content.rect.height*Mathf.Max(.0001f,2*Mathf.Min(uv.y,1-uv.y)));
            settings.InitialScale=Mathf.Clamp(Mathf.Max(settings.InitialScale,horizontal*1.01f,vertical*1.01f),settings.MinScale,settings.MaxScale);
            zoomer.ResetAndCenter(settings,uv);
        }
        public static UiReturnFrame Capture(TabletController owner,string profilePersonId)
        {
            MapApp app;LiveSceneTabletMap live;RectTransform content;MapZoomSettings settings;Resolve(owner,out app,out live,out content,out settings);var map=app.GetComponent<HousingMap>();var window=map?map.Window:null;var building=window&&window.Panel?window.Building:null;
            var state=new FriendMapViewState{layer=live.IsUnderwater?1:0,center=CenterFromContent(content.anchoredPosition,content.rect.size,content.localScale.x),zoom=content.localScale.x,fit=settings.MinScale,cardOpen=building!=null,buildingId=building?.id,houseInstanceId=FriendMapBuildingInfo.Identity(building?.house),floor=window?window.Floor:0,homeVisit=FriendHousingUI.HasHomeVisit,visitorId=FriendHousingUI.VisitPersonId,worldId=FriendsLifeRuntime.Instance?.State?.worldId};
            var selected=UnityEngine.EventSystems.EventSystem.current?UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject:null;
            return new UiReturnFrame{app="Map",page="Map",personId=profilePersonId,purpose="ProfileFromMap",entityId=building!=null?FriendMapBuildingInfo.Entity(building.id,building.house):"Layer/"+state.layer,mapView=state,focus=selected,focusName=selected?selected.name:""};
        }
        public static HousingMap Restore(TabletController owner,FriendMapViewState state)
        {
            if(state==null)throw new InvalidOperationException("Captured map view absent");var world=FriendsLifeRuntime.Instance?.State?.worldId;if(!string.IsNullOrEmpty(state.worldId)&&state.worldId!=world)throw new InvalidOperationException("Map view belongs to another world");
            MapApp app;LiveSceneTabletMap live;RectTransform content;MapZoomSettings settings;var zoomer=Resolve(owner,out app,out live,out content,out settings);Layer(app,live,state.layer);zoomer=Resolve(owner,out app,out live,out content,out settings);settings.InitialScale=RestoredZoom(state.zoom,state.fit,settings.MinScale,settings.MaxScale);zoomer.ResetAndCenter(settings,state.center);
            var map=app.GetComponent<HousingMap>();if(map&&map.Window)map.Window.Close();
            if(map&&state.cardOpen){var info=FriendMapBuildingInfo.Find(state.buildingId,state.houseInstanceId);if(info!=null)FriendMapBuildingInfo.Open(map,info,state.visitorId,state.floor);}
            return map;
        }
    }
}
