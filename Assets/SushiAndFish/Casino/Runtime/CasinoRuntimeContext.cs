using System;
using System.IO;
using UnityEngine;

namespace SushiAndFish.Casino.Original
{
    // The host owns the only spendable wallet. Mirror's long is a synchronized
    // view used by the unchanged original machine code, never a saved bankroll.
    public interface ICasinoGoldWallet
    {
        long WholeCoins { get; }
        bool TryChangeCoins(long delta, out long resultingWholeCoins);
    }

    public static class CasinoRuntimeContext
    {
        public const bool IsHostIntegration = true;
        public static bool Active;
        public static ICasinoGoldWallet Wallet;
        public static Action<GameObject> SceneLoaded;
        public static Action<Vector3> PlayerTeleported;
        public static Action<Vector2> PlayerRotated;
        public static Action<Transform, int> ButtonPressed;
        public static Action<int> FloorChanging;
        public static Func<Transform, bool> IsCasinoButton;
        public static Func<bool> CanInteract;
        public static Action SharedShadersRefresh;
        public static Func<Vector3> PlayerPosition;
        public static Func<Vector2> PlayerLook;
        public static Camera PlayerCamera;
        public static Transform PlayerRoot;
        public static long ReadCoins => Wallet == null ? 0 : Wallet.WholeCoins;
    }

    public static class CasinoPaths
    {
        public static string PersistentDataPath
        {
            get
            {
                var path = Path.Combine(Application.persistentDataPath, "CasinoState");
                Directory.CreateDirectory(path);
                return path;
            }
        }
        public static string StreamingAssetsPath => Path.Combine(Application.streamingAssetsPath, "Casino");
    }

    public static class CasinoResources
    {
        private static string PathOf(string path) => "Casino/" + path;
        public static T Load<T>(string path) where T : UnityEngine.Object => UnityEngine.Resources.Load<T>(PathOf(path));
        public static UnityEngine.Object Load(string path) => UnityEngine.Resources.Load(PathOf(path));
        public static UnityEngine.Object Load(string path, Type type) => UnityEngine.Resources.Load(PathOf(path), type);
        public static T[] LoadAll<T>(string path) where T : UnityEngine.Object => UnityEngine.Resources.LoadAll<T>(PathOf(path));
        public static UnityEngine.Object[] LoadAll(string path) => UnityEngine.Resources.LoadAll(PathOf(path));
        public static T[] FindObjectsOfTypeAll<T>() where T : UnityEngine.Object => UnityEngine.Resources.FindObjectsOfTypeAll<T>();
        public static AsyncOperation UnloadUnusedAssets() => UnityEngine.Resources.UnloadUnusedAssets();
    }
}
