using System;
using UnityEngine;

namespace Framework
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public abstract class UGameInstance : MonoBehaviour
    {
        public static event Action<UGameInstance> GameInstanceInitialized;
        public static event Action<UGameInstance> GameInstanceShuttingDown;

        public static UGameInstance Instance { get; private set; }

        public bool IsInitialized { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"Multiple {nameof(UGameInstance)} instances found. Keeping {Instance.name} and removing {name}.",
                    this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGameInstanceInternal();
        }

        protected virtual void OnApplicationQuit()
        {
            ShutdownGameInstanceInternal();
        }

        protected virtual void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            ShutdownGameInstanceInternal();
            Instance = null;
        }

        public static bool TryGetGameInstance(out UGameInstance gameInstance)
        {
            gameInstance = Instance;
            return gameInstance != null && gameInstance.IsInitialized;
        }

        public static bool TryGetGameInstance<TGameInstance>(out TGameInstance gameInstance)
            where TGameInstance : UGameInstance
        {
            gameInstance = Instance as TGameInstance;
            return gameInstance != null && gameInstance.IsInitialized;
        }

        protected virtual void InitializeGameInstance()
        {
        }

        protected virtual void ShutdownGameInstance()
        {
        }

        private void InitializeGameInstanceInternal()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;
            InitializeGameInstance();
            GameInstanceInitialized?.Invoke(this);
        }

        private void ShutdownGameInstanceInternal()
        {
            if (!IsInitialized)
            {
                return;
            }

            GameInstanceShuttingDown?.Invoke(this);
            ShutdownGameInstance();
            IsInitialized = false;
        }
    }
}
