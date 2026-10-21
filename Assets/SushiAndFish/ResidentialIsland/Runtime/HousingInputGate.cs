using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using UnityEngine.InputSystem;

namespace SushiAndFish.ResidentialIsland
{
    // Scope Escape to the foreground preview/window. Restore only the actions
    // that were enabled beforehand, after Escape is released.
    public sealed class HousingInputGate
    {
        readonly List<InputAction> paused=new List<InputAction>();
        public bool Suppressed {get;private set;}
        public void Acquire(bool placementKeys=false)
        {
            Suppressed=true;
            var asset=Singleton<InputManager>.Instance?.InputHandler?.InputMap?.asset;
            if(asset==null)return;
            foreach(var action in asset)
                if(action.enabled && !paused.Contains(action) && action.bindings.Any(b=>
                {
                    string path=b.effectivePath?.ToLowerInvariant();
                    return path!=null&&(path.Contains("/escape")||placementKeys&&(path=="<keyboard>/w"||path=="<keyboard>/a"||path=="<keyboard>/s"||path=="<keyboard>/d"));
                }))
                {paused.Add(action);action.Disable();}
        }
        public bool Release(bool force=false)
        {
            if(!force&&Keyboard.current?.escapeKey.isPressed==true)return false;
            foreach(var action in paused)action.Enable();paused.Clear();Suppressed=false;return true;
        }
    }
}
