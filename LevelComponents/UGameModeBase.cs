using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine
{
    [DefaultExecutionOrder(-1)]
    public abstract class UGameModeBase : UNetworkedObject
    {
        public class LoadProcess
        {
            /// <summary>
            /// Name of the load process
            /// </summary>
            public string name;

            /// <summary>
            /// The progress of the load process
            /// </summary>
            public float progress;

            public IEnumerator process;

            /// <summary>
            /// Has the current progress reached 100? Used for detecting when to 
            /// proceed loading the next process in GameModeBase
            /// </summary>
            public bool IsDone => progress == 100f;

            /// <summary>
            /// Marks the current load process as completed and will continue if
            /// there are any other processes to load in the GameModeBase
            /// </summary>
            public void Completed() => progress = 100f;
        }

        [Header("Base Settings")]
        [SerializeField] protected InputActionAsset inputContext;
        [SerializeField] protected UController playerController;
        [SerializeField] protected ULevelPawn playerPawn;
        [SerializeField] protected UGameInstance gameInstance;

        protected UController _playerController;
        protected ULevelPawn _playerPawn;
        protected UGameInstance _gameInstance;

        protected List<LoadProcess> loadProcesses;

        public event EventHandler OnPause;
        public event EventHandler OnResume;

        public event EventHandler OnLevelStarted;
        public event EventHandler OnLevelFinished;

        /// <summary>
        /// Event for when the game mode has been initialized and is ready to start loading the level
        /// </summary>
        public event EventHandler OnGameModeInitialized;

        public static UGameModeBase instance { get; private set; }

        public InputActionAsset InputContext => inputContext;
        public UGameInstance GameInstance => _gameInstance;

        protected void Awake()
        {
            instance = this;

            _gameInstance = UGameInstance.singleton;
            if (_gameInstance == null)
            {
                _gameInstance = Instantiate(gameInstance);
            }

            inputContext.Enable();

            loadProcesses = new List<LoadProcess>();
        }

        protected virtual IEnumerator Start()
        {
            OnGameModeInitialized?.Invoke(this, EventArgs.Empty);

            Init();

            yield return StartCoroutine(LoadLevel());

            OnLevelStarted?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void Init()
        {
            _playerController = Instantiate(playerController);
            _playerPawn = Instantiate(playerPawn);
        }

        protected virtual void Update() { }
            
        protected virtual void OnDisable() => CleanUpGameMode();

        protected virtual void CleanUpGameMode()
        {
            inputContext.Disable();

            if (_playerController != null)
            {
                Destroy(_playerController.gameObject);
            }

            if (_playerPawn != null)
            {
                Destroy(_playerPawn.gameObject);
            }

            OnLevelFinished?.Invoke(this, EventArgs.Empty);
        }

        public virtual void PauseGame()
        {
            Time.timeScale = Mathf.Epsilon;

            OnPause?.Invoke(this, EventArgs.Empty);
        }

        public virtual void ResumeGame()
        {
            Time.timeScale = 1f;

            OnResume?.Invoke(this, EventArgs.Empty);
        }

        public void AddLoadingProcess(LoadProcess loadProcess)
        {
            if (loadProcess.process == null) return;

            loadProcesses.Add(loadProcess);
        }

        public virtual IEnumerator LoadLevel()
        {
            for (int i = 0; i < loadProcesses.Count; i++)
            {
                LoadProcess loadprocess = loadProcesses[i];
                StartCoroutine(loadprocess.process);

                while (!loadprocess.IsDone)
                {
                    yield return null;
                }
            }
        }

        public virtual ULevelPawn GetPlayerPawn() => _playerPawn;
        public virtual UController GetPlayerController() => _playerController;

        public virtual T GetPlayerPawn<T>() where T : ULevelPawn => _playerPawn.CastTo<T>();
        public virtual T GetPlayerController<T>() where T : UController => _playerController.CastTo<T>();
        public T GetGameInstance<T>() where T : UGameInstance => _gameInstance.CastTo<T>();
        public T GetGameMode<T>() where T : UGameModeBase => this.CastTo<T>();

        public virtual void LoadScene(int buildIndex)
        {
            CleanUpGameMode();

            SceneManager.LoadScene(buildIndex);
        }

        public virtual void QuitGame()
        {
            Application.Quit();
        }
    }
}