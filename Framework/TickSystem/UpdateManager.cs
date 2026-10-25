using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnscriptedEngine
{
    // Resolve once per concrete type. Only overrides of framework slots count;
    // unrelated hidden methods must not opt a component into dispatch.
    internal static class TickCapabilities
    {
        private static readonly Dictionary<Type, (bool Update, bool Fixed)> cache = new();
        private static readonly Type[] parameters = { typeof(float) };

        internal static (bool Update, bool Fixed) For(Type type, Type root)
        {
            if (cache.TryGetValue(type, out var result)) return result;
            result = (Overrides(type, root, "Tick") || Overrides(type, root, "ServerTick") || Overrides(type, root, "ClientTick"),
                Overrides(type, root, "FixedTick") || Overrides(type, root, "ServerFixedTick") || Overrides(type, root, "ClientFixedTick"));
            cache.Add(type, result);
            return result;
        }

        private static bool Overrides(Type type, Type root, string name)
        {
            for (Type current = type; current != null && current != root; current = current.BaseType)
            {
                var method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public | BindingFlags.DeclaredOnly, null, parameters, null);
                if (method != null && method.IsVirtual && method.GetBaseDefinition().DeclaringType == root)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Central frame/fixed-step dispatch. Lower order runs first; equal orders preserve registration order per phase.
    /// Callbacks added during dispatch start on the next dispatch of that phase;
    /// removals take effect immediately. These are Unity ticks, not NGO network ticks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpdateManager : MonoBehaviour
    {
        private static readonly UpdateChannel updates = new();
        private static readonly UpdateChannel fixedUpdates = new();
        private static UpdateManager instance;
        private static bool quitting;

        public static int UpdateListenerCount => updates.Count;
        public static int FixedUpdateListenerCount => fixedUpdates.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState()
        {
            updates.Clear();
            fixedUpdates.Clear();
            instance = null;
            quitting = false;
        }

        public static void RegisterUpdate(Behaviour owner, Action<float> callback, int order = 0)
        {
            if (!Application.isPlaying || quitting) return;
            EnsureInstance();
            updates.Register(owner, callback, order);
        }

        public static void RegisterFixedUpdate(Behaviour owner, Action<float> callback, int order = 0)
        {
            if (!Application.isPlaying || quitting) return;
            EnsureInstance();
            fixedUpdates.Register(owner, callback, order);
        }

        public static void UnregisterUpdate(Behaviour owner) => updates.Unregister(owner);
        public static void UnregisterFixedUpdate(Behaviour owner) => fixedUpdates.Unregister(owner);

        public static void Unregister(Behaviour owner)
        {
            updates.Unregister(owner);
            fixedUpdates.Unregister(owner);
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;
            new GameObject("UnscriptedEngine Update Manager").AddComponent<UpdateManager>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (instance == this) updates.Dispatch(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (instance == this) fixedUpdates.Dispatch(Time.fixedDeltaTime);
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            // Keep subscriptions if the runner alone is destroyed during gameplay.
            if (!quitting && Application.isPlaying && (updates.Count > 0 || fixedUpdates.Count > 0))
                EnsureInstance();
        }
    }
}
