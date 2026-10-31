using System;using System.Linq;using System.Collections.Generic;using System.Reflection;using UnityEngine;using DG.Tweening;
using HostMusic=SushiAndFish.Casino.Original.MusicManager;
using HostRadio=PaperPixelGames.Core.Radio;
using HostShopMusic=PaperPixelGames.Core.Shop.ShopMusicHandler;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    // Explicit current-host BGM counterpart. The real existing MusicManager
    // owns FMOD playback; shop/radio own their authored AudioSources. Ambient,
    // dialogue, fridge, rain and other SFX are not promoted to BGM by isPlaying.
    public static class NativeHostBgmTransitions
    {
        static AudioSource FieldSource(Component owner,string name)
        {
            var field=owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(field==null||field.FieldType!=typeof(AudioSource))throw new OriginalBindingException("Actual authored host BGM AudioSource field unavailable: "+name);return field.GetValue(owner)as AudioSource;
        }
        static bool SourceTransition(AudioSource source)
        {
            if(!source||!source.enabled||!source.gameObject.activeInHierarchy)throw new OriginalBindingException("Actual enabled host BGM AudioSource unavailable");
            if(source.clip&&source.clip.loadState==AudioDataLoadState.Failed)throw new OriginalBindingException("Actual host BGM clip failed loading");
            if(source.clip&&source.clip.loadState==AudioDataLoadState.Loading)return true;
            var tweens=DOTween.TweensByTarget(source,false);return tweens!=null&&tweens.Any(t=>t!=null&&t.IsActive()&&!t.IsComplete());
        }
        public static NativeBgmObservation Observe()
        {
            var music=HostMusic.Instance;if(!music||!music.isActiveAndEnabled)throw new OriginalBindingException("Actual existing global host MusicManager required");
            var emitter=music.GetComponent<FMODUnity.StudioEventEmitter>();if(!emitter||!emitter.isActiveAndEnabled)throw new OriginalBindingException("Actual global MusicManager FMOD emitter unavailable");
            bool transitioning=false;var detail=new List<string>();var instance=emitter.EventInstance;
            if(!instance.isValid())
            {
                // enableMusic is the original controller configuration, not an
                // active playback/transition record. Original0205588c ignores
                // null entries and reports false for an empty active BGM list.
                // IsActive records a real emitter Play without a later Stop.
                if(emitter.IsActive)throw new OriginalBindingException("Actual active BGM emitter has no valid playback instance; its requested resource/start owner is unavailable");
                detail.Add("GlobalMusic/NoActiveInstance/EmitterInactive");
            }
            else
            {
                FMOD.Studio.PLAYBACK_STATE state;var result=instance.getPlaybackState(out state);if(result!=FMOD.RESULT.OK)throw new OriginalBindingException("Actual BGM playback read failed: "+result);
                if(state==FMOD.Studio.PLAYBACK_STATE.STARTING||state==FMOD.Studio.PLAYBACK_STATE.STOPPING)transitioning=true;
                else if(state!=FMOD.Studio.PLAYBACK_STATE.PLAYING&&state!=FMOD.Studio.PLAYBACK_STATE.SUSTAINING&&state!=FMOD.Studio.PLAYBACK_STATE.STOPPED)throw new OriginalBindingException("Unknown actual FMOD music state");detail.Add("GlobalMusic/"+state);
            }
            foreach(var shop in UnityEngine.Object.FindObjectsByType<HostShopMusic>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(x=>x.isActiveAndEnabled)){var source=FieldSource(shop,"_loopEngineSource");transitioning|=SourceTransition(source);detail.Add("AuthoredShopMusic/"+source.GetInstanceID());}
            foreach(var radio in UnityEngine.Object.FindObjectsByType<HostRadio>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(x=>x.isActiveAndEnabled))
            {
                var source=FieldSource(radio,"_audioSource");if(!source)throw new OriginalBindingException("Actual Radio music source unavailable");if(radio.IsPlaying&&!source.clip)throw new OriginalBindingException("Requested Radio track has no actual clip owner");transitioning|=SourceTransition(source);detail.Add("AuthoredRadioMusic/"+source.GetInstanceID());
            }
            // This live scene had no boss music controller. Its independent
            // MoveTowards crossfade must not be silently counted stable later.
            if(UnityEngine.Object.FindObjectsByType<PaperPixelGames.Core.Boss.CrabBossPresentationController>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Any(x=>x.isActiveAndEnabled))throw new OriginalBindingException("Active boss BGM producer requires its exact crossfade target owner");
            return new NativeBgmObservation{resolved=true,transitioning=transitioning,owner="ActualHostMusicManager+AuthoredShop+Radio/v2-InactiveEmitterIsAbsent",detail=string.Join(";",detail)};
        }
    }
}
