using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiAndFish.FriendsLife
{
    // Only an explicitly opened NEW friends modal owns this scope. The normal
    // original New Game creator never constructs it. No NGO private state.
    internal sealed class FriendCreatorNetworkSceneScope:IDisposable
    {
        readonly string sceneName;
        readonly HashSet<int> existing=new HashSet<int>();
        readonly NetworkSceneManager networkScenes;
        readonly NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler previous,verify;
        Scene owned;bool disposed;
        public FriendCreatorNetworkSceneScope(string name)
        {
            sceneName=name;
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var s=SceneManager.GetSceneAt(i);existing.Add(s.handle);
                if(s.name==name&&s.isLoaded)throw new InvalidOperationException("请先关闭捏人界面。");
            }
            var network=NetworkManager.Singleton;
            if(network&&network.IsServer&&network.IsListening)networkScenes=network.SceneManager;
            if(networkScenes!=null)
            {
                previous=networkScenes.VerifySceneBeforeLoading;
                verify=Verify;networkScenes.VerifySceneBeforeLoading=verify;
            }
            SceneManager.sceneLoaded+=Loaded;
        }
        void Loaded(Scene scene,LoadSceneMode mode)
        {if(!disposed&&mode==LoadSceneMode.Additive&&scene.name==sceneName&&!existing.Contains(scene.handle))owned=scene;}
        public void Claim(Scene scene)
        {
            if(disposed||!scene.IsValid()||!scene.isLoaded||scene.name!=sceneName||existing.Contains(scene.handle))throw new InvalidOperationException("无法打开外貌编辑，请重试。");
            owned=scene;
        }
        bool Verify(int buildIndex,string name,LoadSceneMode mode)
        {
            // Invoke the complete original delegate, preserving its effective
            // policy and all listeners; += would let the final bool override it.
            bool allowed=previous==null||previous(buildIndex,name,mode);
            if(!allowed||disposed||!owned.IsValid()||!owned.isLoaded)return allowed;
            return !(owned.name==name&&owned.buildIndex==buildIndex);
        }
        public void MoveIfUnparented(GameObject value)
        {if(value&&!value.transform.parent&&owned.IsValid()&&owned.isLoaded&&value.scene!=owned)SceneManager.MoveGameObjectToScene(value,owned);}
        public void Dispose()
        {
            if(disposed)return;disposed=true;SceneManager.sceneLoaded-=Loaded;
            if(networkScenes==null||verify==null)return;
            var current=networkScenes.VerifySceneBeforeLoading;
            if(current==verify){networkScenes.VerifySceneBeforeLoading=previous;return;}
            // Preserve later listeners while expanding our wrapper back into
            // its captured original invocation list at the same position.
            if(current==null)return;Delegate restored=null;bool found=false;
            foreach(var entry in current.GetInvocationList())
            {
                if(entry.Equals(verify)){found=true;if(previous!=null)foreach(var prior in previous.GetInvocationList())restored=Delegate.Combine(restored,prior);}
                else restored=Delegate.Combine(restored,entry);
            }
            if(found)networkScenes.VerifySceneBeforeLoading=(NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler)restored;
        }
    }
}
