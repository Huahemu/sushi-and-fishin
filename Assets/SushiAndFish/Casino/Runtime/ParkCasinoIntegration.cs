using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Input;
using SushiAndFish.Casino.Original;
using SushiAndFish.Casino.Original.CasinoLab;
using SushiAndFish.Casino.Original.Extensions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using PaperPixelGames.Networking;
using HostNetworkManager = Unity.Netcode.NetworkManager;
using OriginalPlayer = SushiAndFish.Casino.Original.PlayerController;

namespace SushiAndFish.Casino
{
    // Scene geometry is transformed only while the original elevator doors are
    // closed. Native game floors retain their authored scale and positions.
    [DefaultExecutionOrder(29000)]
    public sealed class ParkCasinoIntegration : MonoBehaviour, ICasinoGoldWallet
    {
        public static ParkCasinoIntegration Instance { get; private set; }
        public CustomNetworkManager networkPrefab;
        public GameObject supportServicesPrefab;
        public GameObject permanentExterior;
        public CasinoElevatorConversation elevatorConversation;
        public Vector3 parkOrigin = new Vector3(-101f, 2.9055f, -72f);
        public float exteriorScale = .47f;
        public float parkYaw = -90f;
        public Material interiorSkybox;
        CasinoInteriorEnvironment interiorEnvironment;
        public Vector3 nativeMapOrigin = new Vector3(10000f, 1000f, 0f);
        public bool Ready { get; private set; }
        public int CurrentFloor { get; private set; }
        public Transform ModuleRoot { get; private set; }
        public PlayerInteract LogicInteractor { get; private set; }
        public PPG_Player HostPlayer { get; private set; }
        public EconomyHandler HostEconomy { get; private set; }
        public CasinoLabBootstrap Bootstrap { get; private set; }
        public int PressCount { get; private set; }
        public float CurrentScale => CurrentFloor == 0 ? exteriorScale : 1f;
        public long WholeCoins => HostEconomy ? HostEconomy.CasinoGoldCoins : 0;
        readonly List<LightGrid> lightGrids = new List<LightGrid>();
        readonly List<Collider> nativeSupportPlanes = new List<Collider>();
        readonly List<Collider> nativeLobbyRegions = new List<Collider>();
        readonly HashSet<Transform> buttons = new HashSet<Transform>();
        readonly HashSet<Canvas> suppressedCanvases = new HashSet<Canvas>();
        readonly List<AttendantPose> attendantPoses=new List<AttendantPose>();
        struct AttendantPose {public NPC actor;public Vector3 position;public Quaternion rotation;}
        readonly List<AttendantTransform> attendantTransforms=new List<AttendantTransform>();
        struct AttendantTransform {public Transform target;public Vector3 position;public Quaternion rotation;}
        OriginalPlayer proxy;
        CasinoButtonHandPress handPress;
        Camera worldCamera;
        int hostCameraMask;
        LayerMask hostMovementCollisionMask;
        bool movementMaskInstalled;
        int casinoCameraMask;
        int exteriorCameraMask;
        int hostDynamicCameraLayers;
        FMODUnity.StudioListener ownedStudioListener;
        FMODUnity.RuntimeManager ownedAudioManager;
        Scene hostScene;
        bool followHost;
        bool hostFollowsSharedFloor;
        // The host renders/plays a native floor only while it actually rode
        // the shared map there. Stuck recovery or an NPC-only ride leaves the
        // host on the island with the source interior environment disabled.
        public bool HostOnNativeFloor => Ready && CurrentFloor != 0 && hostFollowsSharedFloor;
        bool HostDetached => CurrentFloor != 0 && !hostFollowsSharedFloor;
        readonly List<Bounds> parkLobbyBounds = new List<Bounds>();
        float hostFarSince = -1f, nextLobbyRecall;
        string lobbyRecallWait;
        const float NativeMapDetachDistance = 2000f;
        public bool HostUsesCurrentNativeMap
        {
            get
            {
                if(!Ready||!HostPlayer||!ModuleRoot)return false;
                if(Vector3.Distance(HostPlayer.transform.position,ModuleRoot.position)>200)return false;
                return nativeLobbyRegions.Any(c=>c&&c.bounds.Contains(HostPlayer.transform.position+Vector3.up*.5f))||
                    Physics.RaycastAll(HostPlayer.transform.position+Vector3.up,Vector3.down,2f,~0,QueryTriggerInteraction.Ignore)
                    .Any(h=>h.collider&&h.collider.transform.IsChildOf(ModuleRoot));
            }
        }
        bool releasingInteraction;
        bool releasingUse;
        bool releasingThrow;
        bool quitting;
        string targetName;
        struct LightGrid { public LightVolume volume; public Vector3 offset; public float spacing; }

        void Awake() { Instance = this; }
        IEnumerator Start()
        {
            while (!HostNetworkManager.Singleton || !HostNetworkManager.Singleton.IsServer ||
                   !Singleton<PlayersManager>.Instance || !Singleton<PlayersManager>.Instance.LocalPlayer)
                yield return null;
            HostPlayer = Singleton<PlayersManager>.Instance.LocalPlayer;
            while (!HostPlayer.IsSpawned || !HostPlayer.FirstPersonCharacter) yield return null;
            while (!FindObjectsByType<GameStartGate>(FindObjectsSortMode.None).Any(g=>g.IsGameStarted&&g.BootstrapCompletedClientCount>=1)) yield return null;
            HostEconomy = FindObjectsByType<EconomyHandler>(FindObjectsSortMode.None).FirstOrDefault(e => e.IsServer && e.IsSpawned);
            if (!HostEconomy) throw new InvalidOperationException("The casino requires the host's spawned gold wallet.");
            hostScene = gameObject.scene;
            worldCamera = HostPlayer.CinemachineBrain ? HostPlayer.CinemachineBrain.GetComponent<Camera>() : Camera.main;
            if (!worldCamera) throw new InvalidOperationException("The casino requires the host world camera.");
            hostCameraMask=worldCamera.cullingMask;
            hostDynamicCameraLayers=LayerMask.GetMask("Player","ViewModel");
            // The imported camera's mask was remapped with the source assets.
            // Keep its room/screen layers while hiding the host's own body.
            int nativeMask=CasinoResources.Load<Camera>("Camera").cullingMask&~((1<<6)|(1<<7)|(1<<10));
            casinoCameraMask=(hostCameraMask|nativeMask)&~(1<<1);
            exteriorCameraMask=hostCameraMask;
            if(permanentExterior)foreach(var renderer in permanentExterior.GetComponentsInChildren<Renderer>(true))exteriorCameraMask|=(1<<renderer.gameObject.layer)&nativeMask;
            worldCamera.cullingMask=exteriorCameraMask;
            hostMovementCollisionMask=HostPlayer.FirstPersonCharacter.characterMovement.collisionLayers;
            HostPlayer.FirstPersonCharacter.characterMovement.collisionLayers=hostMovementCollisionMask|(1<<CasinoLayers.NameToLayer("Ground"));
            movementMaskInstalled=true;
            HostPlayer.Interactor.SetInteractRange(4);
            interiorEnvironment=worldCamera.gameObject.AddComponent<CasinoInteriorEnvironment>();
            interiorEnvironment.casino=this;interiorEnvironment.sourceSky=interiorSkybox;
            handPress = HostPlayer.gameObject.AddComponent<CasinoButtonHandPress>();
            handPress.player = HostPlayer;
            CasinoRuntimeContext.Active = true;
            CasinoRuntimeContext.SharedShadersRefresh=OriginalNativeShaderRestoration.RequestRefresh;
            CasinoLayers.ConfigurePhysics();
            RecoveryShaderSupport.InstallForHost();
            CasinoAudioBridge.Prepare();
            var priorAudio=FindFirstObjectByType<FMODUnity.RuntimeManager>();
            if(!worldCamera.GetComponent<FMODUnity.StudioListener>())ownedStudioListener=worldCamera.gameObject.AddComponent<FMODUnity.StudioListener>();
            if(!priorAudio)ownedAudioManager=FindFirstObjectByType<FMODUnity.RuntimeManager>();
            CasinoRuntimeContext.Wallet = this;
            CasinoRuntimeContext.PlayerCamera = worldCamera;
            CasinoRuntimeContext.PlayerRoot = HostPlayer.transform;
            CasinoRuntimeContext.PlayerPosition = () => HostPlayer.transform.position + Vector3.up * (.4f * CurrentScale);
            CasinoRuntimeContext.PlayerLook = () => new Vector2(worldCamera.transform.eulerAngles.y, worldCamera.transform.eulerAngles.x);
            CasinoRuntimeContext.SceneLoaded = OnCasinoRootLoaded;
            CasinoRuntimeContext.PlayerTeleported = OnOriginalTeleport;
            CasinoRuntimeContext.PlayerRotated = OnOriginalRotate;
            CasinoRuntimeContext.FloorChanging = ChangeMapForFloor;
            CasinoRuntimeContext.IsCasinoButton = t => t && buttons.Contains(t);
            CasinoRuntimeContext.CanInteract = () => Ready && HostInputAvailable;
            CasinoRuntimeContext.ButtonPressed = (button, hand) => { PressCount++; handPress.Press(button, hand); };
            HostEconomy.ExactGoldChanged += SynchronizeWallet;
            CasinoLabBootstrap.Ready += OnCasinoReady;
            // Start/Awake on the imported prefab are deliberately not installed
            // in the host scene; its native manager references are copied here.
            var boot = new GameObject("OriginalCasinoBootstrap");
            Bootstrap = boot.AddComponent<CasinoLabBootstrap>();
            Bootstrap.networkPrefab = networkPrefab;
            Bootstrap.supportServicesPrefab = supportServicesPrefab;
            while (!Ready) { SuppressOriginalHUD(); yield return null; }
        }

        bool HostInputAvailable => (CurrentFloor==0||hostFollowsSharedFloor) && HostPlayer && Singleton<InputManager>.Instance && Singleton<InputManager>.Instance.InputHandler &&
            Singleton<InputManager>.Instance.InputHandler.GameplayInputEnabled &&
            !HostPlayer.IsTabletActive && !HostPlayer.IsToolActive && !HostPlayer.Interactor.IsHolding && !HostPlayer.Interactor.IsUsing;

        public bool TryChangeCoins(long delta, out long resultingWholeCoins)
        {
            if (!HostEconomy) { resultingWholeCoins = 0; return false; }
            return HostEconomy.ServerTryChangeCasinoCoins(delta, out resultingWholeCoins);
        }
        void SynchronizeWallet()
        {
            var manager = NetworkSingleton<MoneyManager>.Instance;
            if (manager && Mirror.NetworkServer.active) manager.HostSynchronizeBalance(WholeCoins);
        }

        void OnCasinoRootLoaded(GameObject root)
        {
            foreach(var attendant in root.GetComponentsInChildren<ElevatorGuy>(true))
            {
                foreach(var t in attendant.GetComponentsInChildren<Transform>(true))
                {
                    attendantTransforms.Add(new AttendantTransform{target=t,position=t.localPosition,rotation=t.localRotation});
                    // Source cosmetics share its player layer. They belong to
                    // this static NPC and must not follow host self-culling.
                    if(t.gameObject.layer==6||t.gameObject.layer==7)t.gameObject.layer=0;
                }
                foreach(var agent in attendant.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))agent.enabled=false;
                foreach(var body in attendant.GetComponentsInChildren<Rigidbody>(true))body.isKinematic=true;
                foreach(var actor in attendant.GetComponentsInChildren<NPC>(true))
                    attendantPoses.Add(new AttendantPose{actor=actor,position=actor.transform.localPosition,rotation=actor.transform.localRotation});
            }
            if (!ModuleRoot)
            {
                ModuleRoot = new GameObject("ParkCasinoNativeMap").transform;
                SceneManager.MoveGameObjectToScene(ModuleRoot.gameObject, root.scene);
                ModuleRoot.position = nativeMapOrigin;
            }
            root.transform.SetParent(ModuleRoot, false);
            CasinoHostRepair.TrimExterior(root);
            foreach(var system in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))system.enabled=false;
            foreach(var module in root.GetComponentsInChildren<UnityEngine.EventSystems.BaseInputModule>(true))module.enabled=false;
            foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            foreach (var grid in root.GetComponentsInChildren<LightVolume>(true))
            {
                lightGrids.Add(new LightGrid { volume = grid, offset = grid.gridOffset, spacing = grid.raySpacing });
                grid.gridOffset = ModuleRoot.TransformPoint(grid.gridOffset);
            }
            // The source plane supports the indoor rooms as well as the
            // exterior. At the park only the town terrain should extend beyond
            // the building; native indoor maps retain their original support.
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.name == "Ground" && collider.transform.parent && collider.transform.parent.name == "Colliders")
                { nativeSupportPlanes.Add(collider);collider.enabled=CurrentFloor!=0; }
                if(collider.name=="SFXLobbyTrigger")nativeLobbyRegions.Add(collider);
            }
        }

        void OnCasinoReady()
        {
            var local = MonoSingleton<LocalManager>.Instance;
            var identity = Mirror.NetworkClient.localPlayer;
            if (!identity || !ModuleRoot) throw new InvalidOperationException("Original casino initialization did not produce a logical player/map.");
            proxy = identity.GetComponent<OriginalPlayer>();
            LogicInteractor = identity.GetComponent<PlayerInteract>();
            foreach (var body in identity.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var collider in identity.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in identity.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var canvas in identity.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            foreach (var hands in identity.GetComponentsInChildren<CasinoLabHandsOnly>(true)) hands.enabled = false;
            foreach (var breathing in identity.GetComponentsInChildren<CameraBreathing>(true)) breathing.enabled = false;
            foreach (var camera in identity.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            local.mainCamera = worldCamera;
            CasinoLabBootstrap.Set(LogicInteractor, "_cam", worldCamera);
            // The native layer indices were remapped on import. All geometry
            // may occlude the ray; only original IInteractable types are picked.
            LogicInteractor.raycastLayer = ~((1<<6)|(1<<7)|(1<<10));
            RegisterCasinoButtons();
            CurrentFloor = 0;
            ApplyMapPose();
            foreach(var pose in attendantPoses)
            {
                if(!pose.actor)continue;
                if(NetworkSingleton<NPCController>.TryGetInstance(out var controller))controller.UnregisterNPC(pose.actor);
                pose.actor.StopAllCoroutines();pose.actor.enabled=false;
                foreach(var agent in pose.actor.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))agent.enabled=false;
                foreach(var body in pose.actor.GetComponentsInChildren<Rigidbody>(true))body.isKinematic=true;
                pose.actor.transform.localPosition=pose.position;pose.actor.transform.localRotation=pose.rotation;
            }
            Physics.SyncTransforms();
            // Ground lobby volume at its park pose; used while the shared map
            // is on a native floor and the island only shows the exterior.
            parkLobbyBounds.Clear();
            foreach(var region in nativeLobbyRegions)if(region&&region.enabled&&region.bounds.size.sqrMagnitude>0)parkLobbyBounds.Add(region.bounds);
            followHost = true;
            Ready = true;
            SceneManager.SetActiveScene(hostScene);
            SuppressOriginalHUD();
            SynchronizeWallet();
            Debug.Log("PARK_CASINO_READY floors=0..5 gold=" + WholeCoins + " buttons=" + buttons.Count + " parkLobbyRegions=" + parkLobbyBounds.Count);
        }

        public void RegisterCasinoButtons()
        {
            buttons.Clear();
            foreach (var behaviour in ModuleRoot.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour is IInteractable && !(behaviour is Item) &&
                    (behaviour.GetType().Name.IndexOf("button", StringComparison.OrdinalIgnoreCase) >= 0 || behaviour is MinesweeperTile ||
                     (behaviour is InteractableEventTrigger && (behaviour.GetComponentInParent<Keypad>() || behaviour.GetComponentInParent<GameBase>())) ||
                     behaviour.name.IndexOf("button", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     behaviour.name.IndexOf("lever", StringComparison.OrdinalIgnoreCase) >= 0 || behaviour.name.IndexOf("bet", StringComparison.OrdinalIgnoreCase) >= 0))
                    buttons.Add(behaviour.transform);
            if(NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator))
                foreach(var button in CasinoLabBootstrap.Field<List<Transform>>(elevator,"buttonList"))if(button)buttons.Add(button);
        }

        void ChangeMapForFloor(int destination)
        {
            if (!Ready || !ModuleRoot) return;
            var pointInMap = ModuleRoot.InverseTransformPoint(HostPlayer.transform.position);
            var yaw = worldCamera.transform.eulerAngles.y-ModuleRoot.eulerAngles.y;
            // A host that already left the native map (stuck recovery) is not
            // carried by later original transfers, e.g. the casino day end.
            bool detached = HostDetached;
            CurrentFloor = destination;
            hostFollowsSharedFloor=!detached&&SushiAndFish.FriendsLife.CasinoCore.NativeCasinoTransportTransaction.IncludesOriginalHumans;
            ApplyMapPose();
            hostFarSince=-1f;
            if(hostFollowsSharedFloor)
            {
                HostPlayer.TeleportPlayer(ModuleRoot.TransformPoint(pointInMap), Quaternion.Euler(0, yaw+ModuleRoot.eulerAngles.y, 0), true);
                HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);
            }
            else if(destination==0&&HostInParkLobby())
            {
                // The island exterior copy has no collision. Put a host who
                // called the map back onto the returned lobby floor.
                var from=HostPlayer.transform.position+Vector3.up*2f;
                foreach(var hit in Physics.RaycastAll(from,Vector3.down,4f,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                    if(hit.collider&&hit.collider.transform.IsChildOf(ModuleRoot)&&hit.normal.y>.7f)
                    {if(hit.point.y>HostPlayer.transform.position.y+.01f){HostPlayer.TeleportPlayer(hit.point,true);HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);}break;}
            }
            SushiAndFish.FriendsLife.CasinoCore.NativeCasinoTransportTransaction.OnOriginalMapPoseChanged(ModuleRoot,destination);
            SyncProxy();
            Debug.Log("PARK_CASINO_MAP_SWITCH floor=" + destination + " nativeScale=" + CurrentScale);
        }

        void ApplyMapPose()
        {
            ModuleRoot.SetPositionAndRotation(CurrentFloor == 0 ? parkOrigin : nativeMapOrigin, Quaternion.Euler(0,CurrentFloor==0?parkYaw:0,0));
            ModuleRoot.localScale = Vector3.one * CurrentScale;
            // Relocating the native map must also refresh skinned bounds.
            // Offscreen culling otherwise leaves the elevator actor at its
            // authored coordinates until a camera happens to see that spot.
            foreach(var skin in ModuleRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
            foreach(var animator in ModuleRoot.GetComponentsInChildren<Animator>(true))animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var plane in nativeSupportPlanes)if(plane)plane.enabled=CurrentFloor!=0;
            UpdateCameraLayers();
            foreach (var grid in lightGrids)
                if (grid.volume) { grid.volume.gridOffset = ModuleRoot.TransformPoint(grid.offset); grid.volume.raySpacing = grid.spacing * CurrentScale; }
            if (permanentExterior) permanentExterior.SetActive(CurrentFloor != 0);
            Physics.SyncTransforms();
            if(MonoSingleton<LightbakerManager>.Instance && MonoSingleton<LightbakerManager>.Instance.targetLightVolume)MonoSingleton<LightbakerManager>.Instance.UpdateActiveLightVolume();
        }

        void OnOriginalTeleport(Vector3 point)
        {
            // Bootstrap also positions its logical player. It must not move a
            // town player to the casino on startup.
            if (!followHost || !Ready || HostDetached) return;
            HostPlayer.TeleportPlayer(point - Vector3.up * (.4f * CurrentScale), true);
            HostPlayer.FirstPersonCharacter.SetVelocity(Vector3.zero);
        }
        void OnOriginalRotate(Vector2 rotation)
        {
            if (!followHost || !Ready || HostDetached) return;
            HostPlayer.FirstPersonCharacter.TeleportRotation(Quaternion.Euler(0, rotation.x, 0));
            HostPlayer.FirstPersonCharacter.PitchInstant(rotation.y);
        }
        void SyncProxy()
        {
            if (!proxy || !HostPlayer) return;
            proxy.transform.position = HostPlayer.transform.position + Vector3.up * (.4f * CurrentScale);
            proxy.transform.rotation = Quaternion.Euler(0, HostPlayer.transform.eulerAngles.y, 0);
        }

        void Update()
        {
            if (!Ready || !proxy) return;
            CasinoHostRepair.ProtectHostUI(HostPlayer);
            ObserveHostOutsideNativeMap();
            if(HostInputAvailable && Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame && LogicInteractor.TargetInteractable!=null){InputEvents.UpdateInteract(true);releasingInteraction=true;}
            if(Mouse.current!=null && Mouse.current.leftButton.wasReleasedThisFrame && releasingInteraction){InputEvents.UpdateInteract(false);releasingInteraction=false;}
            SyncProxy();
            LogicInteractor.raycastDistance = 4.2f;
            LogicInteractor.raycastRadius = .1f * CurrentScale;
            LogicInteractor.raycastBlockThreshold = .5f * CurrentScale;
            UpdateCameraLayers();
            SuppressOriginalHUD();
            UpdateHostHint();
        }
        void LateUpdate()
        {
            if(!Ready)return;
            SyncProxy();
            foreach(var pose in attendantTransforms)if(pose.target){pose.target.localPosition=pose.position;pose.target.localRotation=pose.rotation;}
        }
        // Stuck recovery (PPG_Player.StuckSave) teleports the host to the
        // island spawn while the single shared map stays on a native floor.
        // The host then no longer follows that floor. Entering the island
        // lobby calls the original elevator back to Ground through the same
        // transaction guards as an NPC ride (rounds settled, passengers back
        // in the cab), so the lobby again has the original cab.
        void ObserveHostOutsideNativeMap()
        {
            if(CurrentFloor==0)return;
            if(hostFollowsSharedFloor)
            {
                bool far=(HostPlayer.transform.position-ModuleRoot.position).sqrMagnitude>NativeMapDetachDistance*NativeMapDetachDistance;
                if(!far){hostFarSince=-1f;return;}
                if(hostFarSince<0f){hostFarSince=Time.unscaledTime;return;}
                if(Time.unscaledTime-hostFarSince<.5f)return;
                hostFollowsSharedFloor=false;hostFarSince=-1f;
                UpdateCameraLayers();
                Debug.Log("PARK_CASINO_HOST_LEFT_NATIVE_FLOOR floor="+CurrentFloor);
            }
            if(Time.unscaledTime<nextLobbyRecall||!HostInParkLobby())return;
            nextLobbyRecall=Time.unscaledTime+1f;
            if(!NetworkSingleton<ElevatorManager>.TryGetInstance(out var elevator))return;
            if(SushiAndFish.FriendsLife.CasinoCore.NativeCasinoTransportTransaction.TryBeginHostLobbyRecall(elevator,out var reason))
                Debug.Log("PARK_CASINO_LOBBY_RECALL from="+CurrentFloor);
            else if(reason!=lobbyRecallWait)Debug.Log("PARK_CASINO_LOBBY_RECALL_WAIT "+reason);
            lobbyRecallWait=reason;
        }
        bool HostInParkLobby()
        {
            var p=HostPlayer.transform.position+Vector3.up*.5f;
            if(parkLobbyBounds.Count==0){var d=p-parkOrigin;d.y=0;return d.sqrMagnitude<12f*12f;}
            foreach(var b in parkLobbyBounds)if(b.Contains(p))return true;
            return false;
        }
        void UpdateCameraLayers()
        {
            if(!worldCamera||!HostPlayer)return;
            bool inside=CurrentFloor!=0&&hostFollowsSharedFloor||nativeLobbyRegions.Any(c=>c&&c.bounds.Contains(HostPlayer.transform.position+Vector3.up*.5f));
            // The host changes these two bits for third person seating,
            // emotes and first person hands. Preserve its current choice.
            int dynamicBits=worldCamera.cullingMask&hostDynamicCameraLayers;
            worldCamera.cullingMask=((inside?casinoCameraMask:exteriorCameraMask)&~hostDynamicCameraLayers)|dynamicBits;
        }

        // The suppressed hierarchies are walked once a second; between walks
        // only the components found by the last walk are re-disabled. Walking
        // every manager and the whole casino module cost ~4-5 ms per frame.
        readonly List<Behaviour> hudBehaviours = new List<Behaviour>();
        readonly List<UnityEngine.UI.Graphic> hudGraphics = new List<UnityEngine.UI.Graphic>();
        float nextHudWalk;
        void SuppressOriginalHUD()
        {
            if (Time.unscaledTime >= nextHudWalk) { nextHudWalk = Time.unscaledTime + 1f; WalkOriginalHUD(); return; }
            foreach (var behaviour in hudBehaviours) if (behaviour && behaviour.enabled) behaviour.enabled = false;
            foreach (var graphic in hudGraphics) if (graphic && graphic.raycastTarget) graphic.raycastTarget = false;
        }
        void WalkOriginalHUD()
        {
            hudBehaviours.Clear(); hudGraphics.Clear();
            var local = MonoSingleton<LocalManager>.Instance;
            if (local) { HideCanvases(local.gameObject);CasinoHostRepair.ProtectHostEnvironment(local.gameObject);
                foreach (var volume in local.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true)) if (volume.name == "PP_MainGame") hudBehaviours.Add(volume); }
            if (NetworkSingleton<GameUI>.TryGetInstance(out var ui)) HideCanvases(ui.gameObject);
            if (NetworkSingleton<GameManager>.TryGetInstance(out var gameManager)) HideCanvases(gameManager.gameObject);
            // DayEnd and other manager overlays are outside LocalManager and
            // GameUI. Their invisible full-screen Graphics can consume clicks
            // on the host's world-space tablet even while no casino UI is shown.
            if(Bootstrap && Bootstrap.HostSupportInstance)HideCanvases(Bootstrap.HostSupportInstance);
            foreach (var root in ModuleRoot ? ModuleRoot.GetComponentsInChildren<Canvas>(true) : Array.Empty<Canvas>())
                if (root.renderMode != RenderMode.WorldSpace && !(root.worldCamera&&root.worldCamera.targetTexture)) { root.enabled = false; suppressedCanvases.Add(root); hudBehaviours.Add(root); }
        }
        void HideCanvases(GameObject root)
        {
            foreach(var camera in root.GetComponentsInChildren<Camera>(true)){camera.enabled=false;hudBehaviours.Add(camera);}
            foreach(var listener in root.GetComponentsInChildren<AudioListener>(true)){listener.enabled=false;hudBehaviours.Add(listener);}
            foreach(var system in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)){system.enabled=false;hudBehaviours.Add(system);}
            foreach(var module in root.GetComponentsInChildren<UnityEngine.EventSystems.BaseInputModule>(true)){module.enabled=false;hudBehaviours.Add(module);}
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.renderMode != RenderMode.WorldSpace) {
                    canvas.enabled = false; suppressedCanvases.Add(canvas); hudBehaviours.Add(canvas);
                    foreach(var raycaster in canvas.GetComponents<UnityEngine.UI.GraphicRaycaster>()){raycaster.enabled=false;hudBehaviours.Add(raycaster);}
                    foreach(var graphic in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)){graphic.raycastTarget=false;hudGraphics.Add(graphic);}
                }
        }
        void UpdateHostHint()
        {
            var target = HostInputAvailable ? LogicInteractor.TargetInteractable : null;
            string next = target != null && target.IsInteractable && target.MeetRequirements ? target.InteractableName : null;
            if (next == targetName) return;
            targetName = next;
            // Reuse the host's existing interaction binding/hint component.
            var hints = Singleton<HintsManager>.Instance;
            if (!hints) return;
            if (string.IsNullOrEmpty(next)) hints.HideExclusiveHint(this);
            else hints.ShowExclusiveHint(this, "Interact", "key_interact");
        }

        public static bool TryConsumeInteraction(InputActionPhase phase)
        {
            var instance = Instance;
            if (!instance || !instance.Ready || !instance.HostInputAvailable || !instance.LogicInteractor) return false;
            var target = instance.LogicInteractor.TargetInteractable;
            if (phase == InputActionPhase.Canceled && instance.releasingInteraction)
            { instance.releasingInteraction = false; InputEvents.UpdateInteract(false); return true; }
            if (target == null) return false;
            if(target is ElevatorGuy elevatorGuy && instance.elevatorConversation)
            {
                if(phase==InputActionPhase.Started)instance.elevatorConversation.Open(elevatorGuy,instance);
                return true;
            }
            if (phase == InputActionPhase.Started)
            { instance.releasingInteraction = true; InputEvents.UpdateInteract(true); }
            return true;
        }

        public static bool TryConsumeHeldItemInput(InputActionPhase phase, bool throwing)
        {
            var instance=Instance;
            if(!instance || !instance.Ready)return false;
            bool pending=throwing?instance.releasingThrow:instance.releasingUse;
            if(phase==InputActionPhase.Canceled && pending)
            {
                if(throwing){instance.releasingThrow=false;InputEvents.UpdateThrowItem(false);}
                else{instance.releasingUse=false;InputEvents.UpdateUseItem(false);}
                return true;
            }
            if(!instance.HostInputAvailable || !instance.proxy || !instance.proxy.GetComponent<PlayerInventory>().NetworkholdingItem)return false;
            if(phase==InputActionPhase.Started)
            {
                if(throwing){instance.releasingThrow=true;InputEvents.UpdateThrowItem(true);}
                else{instance.releasingUse=true;InputEvents.UpdateUseItem(true);}
            }
            return true;
        }

        void OnDestroy()
        {
            if (HostEconomy) HostEconomy.ExactGoldChanged -= SynchronizeWallet;
            CasinoLabBootstrap.Ready -= OnCasinoReady;
            if (Instance == this)
            {
                Ready=false;followHost=false;
                if(interiorEnvironment)Destroy(interiorEnvironment);
                if(movementMaskInstalled&&HostPlayer&&HostPlayer.FirstPersonCharacter)HostPlayer.FirstPersonCharacter.characterMovement.collisionLayers=hostMovementCollisionMask;
                if(worldCamera)worldCamera.cullingMask=hostCameraMask;
                if(!quitting)
                {
                    if(Bootstrap)Bootstrap.ShutdownForHost();
                    if(ownedStudioListener)Destroy(ownedStudioListener);
                    if(ownedAudioManager)Destroy(ownedAudioManager.gameObject);
                }
                CasinoAudioBridge.ReleaseSettings();
                RecoveryShaderSupport.UninstallForHost();
                CasinoRuntimeContext.Active = false;
                CasinoRuntimeContext.Wallet = null;
                CasinoRuntimeContext.PlayerCamera = null;
                CasinoRuntimeContext.PlayerRoot = null;
                CasinoRuntimeContext.SceneLoaded = null;
                CasinoRuntimeContext.PlayerTeleported = null;
                CasinoRuntimeContext.PlayerRotated = null;
                CasinoRuntimeContext.FloorChanging = null;
                CasinoRuntimeContext.IsCasinoButton = null;
                CasinoRuntimeContext.CanInteract = null;
                CasinoRuntimeContext.ButtonPressed = null;
                CasinoRuntimeContext.SharedShadersRefresh = null;
                Instance = null;
            }
        }
        void OnApplicationQuit(){quitting=true;}
    }
}
