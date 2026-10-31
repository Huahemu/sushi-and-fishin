using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using SushiAndFish.ResidentialIsland;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SushiAndFish.FriendsLife
{
    // Owns only the existing handler's tablet callback while an added page owns navigation.
    // The generated action map and all original event subscriptions remain intact.
    public sealed class FriendTabletInputBridge : MonoBehaviour, global::InputMap.ITabletActions
    {
        static readonly FieldInfo Callbacks=typeof(global::InputMap).GetField("m_TabletActionsCallbackInterfaces",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo WindowGate=typeof(HousingInfoWindow).GetField("gate",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo PausedActions=typeof(HousingInputGate).GetField("paused",BindingFlags.Instance|BindingFlags.NonPublic);
        FriendTabletAccess access;InputHandler handler;global::InputMap map;
        HousingInfoWindow suspended;bool savedEnabled,installed;
        HousingInputGate originalGate;InputAction ownedEscape;
        readonly List<InputAction> scopedEscapes=new List<InputAction>();
        static readonly List<InputAction> pendingEscapes=new List<InputAction>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetPending(){pendingEscapes.Clear();}
        static bool EscapeHeld=>Keyboard.current?.escapeKey.isPressed==true||Gamepad.current?.startButton.isPressed==true;
        public static void FlushReleasedEscapes()
        {
            if(EscapeHeld)return;
            foreach(var action in pendingEscapes.ToArray())
            {
                try{if(!action.enabled&&action.actionMap?.enabled==true)action.Enable();}
                catch(ObjectDisposedException){}
                pendingEscapes.Remove(action);
            }
        }
        void ScopeOtherEscapes()
        {
            foreach(var action in map.asset)
                if(action!=map.Tablet.CloseTablet&&action.enabled&&action.bindings.Any(b=>b.effectivePath?.IndexOf("/escape",StringComparison.OrdinalIgnoreCase)>=0))
                {if(!scopedEscapes.Contains(action))scopedEscapes.Add(action);action.Disable();}
        }
        void RestoreOtherEscapes()
        {
            foreach(var action in scopedEscapes)if(!pendingEscapes.Contains(action))pendingEscapes.Add(action);
            scopedEscapes.Clear();FlushReleasedEscapes();
        }
        public bool Installed=>installed;
        public string LastBoundary {get;private set;}
        public void Bind(FriendTabletAccess owner){access=owner;Sync();}
        public void Sync()
        {
            FlushReleasedEscapes();
            var current=Singleton<InputManager>.Instance?.InputHandler;
            if(installed&&(handler!=current||map!=current?.InputMap||!access||!access.OwnsInput))Release();
            if(!installed&&access&&access.OwnsInput&&current&&current.InputMap!=null)
            {
                var list=Callbacks?.GetValue(current.InputMap) as List<global::InputMap.ITabletActions>;
                if(list==null||list.Count!=1||!ReferenceEquals(list[0],current)){LastBoundary="Original tablet callback ownership differs";return;}
                handler=current;map=current.InputMap;map.Tablet.RemoveCallbacks(handler);map.Tablet.AddCallbacks(this);installed=true;LastBoundary=null;
            }
            if(installed)ScopeOtherEscapes();
            var target=installed?FriendHousingUI.NavigationWindow:null;
            if(suspended!=target)
            {
                RestoreWindow();
                if(target)
                {
                    suspended=target;savedEnabled=target.enabled;target.enabled=false;
                    originalGate=WindowGate?.GetValue(target) as HousingInputGate;
                    var paused=originalGate==null?null:PausedActions?.GetValue(originalGate) as List<InputAction>;
                    var escape=map.Tablet.CloseTablet;
                    // The source card pauses all Escape actions. Re-enable only
                    // the one routed by this owner; global Pause stays suppressed.
                    if(originalGate?.Suppressed==true&&paused?.Contains(escape)==true&&!escape.enabled){ownedEscape=escape;escape.Enable();}
                }
            }
        }
        void RestoreWindow()
        {
            if(suspended&&originalGate?.Suppressed==true&&ownedEscape!=null&&ownedEscape.enabled)ownedEscape.Disable();
            ownedEscape=null;originalGate=null;if(suspended)suspended.enabled=savedEnabled;suspended=null;
        }
        void Release()
        {
            RestoreWindow();if(installed&&map!=null){map.Tablet.RemoveCallbacks(this);if(handler&&handler.InputMap==map)map.Tablet.AddCallbacks(handler);}RestoreOtherEscapes();
            installed=false;handler=null;map=null;
        }
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
        bool Owns=>installed&&access&&access.OwnsInput;
        bool EndEditing()
        {
            var field=EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>();
            if(!field||!field.isFocused)return false;field.DeactivateInputField();return true;
        }
        // The source asset binds CloseTablet to Escape and BackTablet to Tab.
        public void OnCloseTablet(InputAction.CallbackContext context)
        {
            if(!Owns){handler?.OnCloseTablet(context);return;}
            if(context.phase!=InputActionPhase.Started)return;
            if(EndEditing()||FriendHousingUI.TryNavigationBack()||access.BackFriends())return;
            access.ReturnToAppGrid();
        }
        public void OnBackTablet(InputAction.CallbackContext context)
        {if(!Owns){handler?.OnBackTablet(context);return;}if(context.phase==InputActionPhase.Started)access.CloseOwnedTablet();}
        public void OnCloseMap(InputAction.CallbackContext context)
        {if(!Owns){handler?.OnCloseMap(context);return;}if(context.phase==InputActionPhase.Started)access.CloseOwnedTablet();}
        public void OnMove(InputAction.CallbackContext context){handler?.OnMove(context);}
        public void OnZoom(InputAction.CallbackContext context){handler?.OnZoom(context);}
        public void OnSprint(InputAction.CallbackContext context){handler?.OnSprint(context);}
        public void OnJump(InputAction.CallbackContext context){handler?.OnJump(context);}
        public void OnLook(InputAction.CallbackContext context){handler?.OnLook(context);}
    }
}
