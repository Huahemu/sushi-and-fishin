using System;
using System.Collections.Generic;
using System.Reflection;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.WorldUISystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife
{
    // Local HUD presentation only. Native seat state/anchors remain authoritative.
    [DefaultExecutionOrder(31000)]
    public sealed class FriendLocalRestMarkerVisibility : MonoBehaviour
    {
        static FriendLocalRestMarkerVisibility instance;
        static readonly FieldInfo Elements=typeof(WorldUIManager).GetField("_elements",BindingFlags.Instance|BindingFlags.NonPublic);
        readonly HashSet<UnityEngine.UI.Graphic> wanted=new HashSet<UnityEngine.UI.Graphic>();
        readonly HashSet<UnityEngine.UI.Graphic> owned=new HashSet<UnityEngine.UI.Graphic>();
        readonly List<UnityEngine.UI.Graphic> scratch=new List<UnityEngine.UI.Graphic>();
        WorldUIManager manager;EmoteArea area;UnityEngine.Object playerIdentity;float nextRefresh;
        int anchorCount=-1,areaCount=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(instance)return;var go=new GameObject("FriendLocalRestMarkerVisibility");DontDestroyOnLoad(go);instance=go.AddComponent<FriendLocalRestMarkerVisibility>();
        }
        void Awake(){if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;}
        void OnEnable(){SceneManager.sceneLoaded+=SceneLoaded;SceneManager.sceneUnloaded+=SceneUnloaded;}
        void SceneLoaded(Scene scene,LoadSceneMode mode){Restore();}
        void SceneUnloaded(Scene scene){Restore();}
        void LateUpdate()
        {
            if(!Singleton<PlayersManager>.TryGetInstance(out var players)||!Singleton<WorldUIManager>.TryGetInstance(out var ui))
            {Restore();return;}
            var player=players.LocalPlayer;
            bool seated=player&&player.IsOwner&&player.IsUsingEmoteArea&&player.CurrentEmoteArea;
            if(!seated){Restore();return;}
            var current=player.CurrentEmoteArea;
            bool changed=manager!=ui||area!=current||playerIdentity!=player;
            if(changed){Restore();manager=ui;area=current;playerIdentity=player;}
            if(Elements==null){Restore();return;}
            if(changed||anchorCount!=ui.RegisteredAnchorCount||areaCount!=EmoteArea.ActiveAreas.Count||Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+1f;anchorCount=ui.RegisteredAnchorCount;areaCount=EmoteArea.ActiveAreas.Count;
                RefreshWanted(ui,current);
            }
            // Source WorldUIElement changes its own active/alpha state, not Graphic.enabled.
            // A pre-disabled graphic is left untouched; only a flag changed here is restored.
            foreach(var graphic in wanted)if(graphic&&graphic.enabled){owned.Add(graphic);graphic.enabled=false;}
        }
        void RefreshWanted(WorldUIManager ui,EmoteArea current)
        {
            wanted.Clear();
            var elements=Elements.GetValue(ui) as Dictionary<WorldUIAnchor,WorldUIElement>;
            if(elements!=null)foreach(var other in EmoteArea.ActiveAreas)
            {
                if(!other||other==current||!other.WorldUIAnchor||!elements.TryGetValue(other.WorldUIAnchor,out var element)||!element)continue;
                foreach(var graphic in element.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))if(graphic)wanted.Add(graphic);
            }
            scratch.Clear();foreach(var graphic in owned)if(!graphic||!wanted.Contains(graphic))scratch.Add(graphic);
            foreach(var graphic in scratch){if(graphic&&!graphic.enabled)graphic.enabled=true;owned.Remove(graphic);}scratch.Clear();
        }
        void Restore()
        {
            foreach(var graphic in owned)if(graphic&&!graphic.enabled)graphic.enabled=true;
            owned.Clear();wanted.Clear();scratch.Clear();manager=null;area=null;playerIdentity=null;anchorCount=areaCount=-1;nextRefresh=0;
        }
        void OnDisable(){SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneUnloaded-=SceneUnloaded;Restore();}
        void OnDestroy(){Restore();if(instance==this)instance=null;}
    }
}
