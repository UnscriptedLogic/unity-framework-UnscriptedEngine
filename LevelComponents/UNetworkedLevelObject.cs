using System;
using System.Collections;
using UnityEngine;

namespace UnscriptedEngine
{
    public abstract class UNetworkedLevelObject : UNetworkedObject, ILevelObject
    {
        private UGameModeBase gameMode;

        protected UGameModeBase GameMode
        {
            get
            {
                if (gameMode == null)
                {
                    gameMode = UGameModeBase.instance;
                }

                return gameMode;
            }
        }

        public static event EventHandler OnObjectCreated;
        public static event EventHandler OnObjectToBeDestroyed;

        protected virtual void Awake()
        {
            FireObjectCreationEvent();

            GameMode.OnLevelStarted += LevelStarted;
            GameMode.OnLevelFinished += LevelStopped;

            AddLoadProcess();
        }

        internal virtual void FireObjectCreationEvent() => OnObjectCreated?.Invoke(this, EventArgs.Empty);
        internal virtual void FireObjectDestroyedEvent() => OnObjectToBeDestroyed?.Invoke(this, EventArgs.Empty);

        internal void GameMode_OnGameModeInitialized(object sender, EventArgs e) => AddLoadProcess();

        private void LevelStarted(object sender, EventArgs eventArgs) => OnLevelStarted();
        private void LevelStopped(object sender, EventArgs eventArgs) => OnLevelStopped();

        /// <summary>
        /// Called when the level has finished loading and the game has started. Is not called when
        /// the object is instantiated after level loading
        /// </summary>
        protected virtual void OnLevelStarted() { }

        /// <summary>
        /// Called when the level has finished and the game is about to be stopped. Is not called when
        /// the object is destroyed before the level is finished
        /// </summary>
        protected virtual void OnLevelStopped() { }

        internal virtual void AddLoadProcess()
        {
            UGameModeBase.LoadProcess loadProcess = new UGameModeBase.LoadProcess();
            loadProcess.name = $"Loading... {name}";
            loadProcess.process = AddToLevelLoading(loadProcess);

            GameMode.AddLoadingProcess(loadProcess);
        }

        protected virtual IEnumerator AddToLevelLoading(UGameModeBase.LoadProcess process)
        {
            yield return null;

            process.Completed();
        }

        public virtual ICanvasController AttachUIWidget(GameObject widget)
        {
            GameObject uiWidget = Instantiate(widget, transform);
            ICanvasController canvasController = uiWidget.GetComponent<ICanvasController>();
            canvasController.OnWidgetAttached(this);
            return canvasController;
        }

        public virtual T AttachUIWidget<T>(T widget) where T : ICanvasController
        {
            T canvasController = Instantiate(widget.gameObject, transform).GetComponent<T>();
            canvasController.OnWidgetAttached(this);
            return canvasController;
        }

        public virtual void DettachUIWidget(GameObject widget) 
        {
            ICanvasController uCanvasController = widget.GetComponent<ICanvasController>();
            uCanvasController.OnWidgetDetached(this);

            Destroy(widget);
        }

        public virtual void DettachUIWidget<T>(T widget) where T : ICanvasController
        {
            T uCanvasController = widget.gameObject.GetComponent<T>();
            uCanvasController.OnWidgetDetached(this);

            Destroy(widget.gameObject);
        }

        public override void OnDestroy()
        {
            GameMode.OnLevelStarted -= LevelStarted;
            GameMode.OnLevelFinished -= LevelStopped;

            FireObjectDestroyedEvent();

            base.OnDestroy();
        }

        public Vector3 SnapToGrid(Vector3 position, float cellSize, Vector3 gridOrigin)
        {
            float snappedX = Mathf.Floor((position.x - gridOrigin.x) / cellSize) * cellSize + gridOrigin.x;
            float snappedY = Mathf.Floor((position.y - gridOrigin.y) / cellSize) * cellSize + gridOrigin.y;
            float snappedZ = Mathf.Floor((position.z - gridOrigin.z) / cellSize) * cellSize + gridOrigin.z;

            return new Vector3(snappedX, snappedY, snappedZ);
        }
    }  
}
