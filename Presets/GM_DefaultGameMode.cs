using System.Collections;
using UnityEngine;
using UnscriptedEngine;

public class GM_DefaultGameMode : UGameModeBase
{
    [System.Serializable]
    public class SpawnSettings
    {
        public Transform spawnAnchor;

        public void ApplySpawnAnchor(Transform spawnee)
        {
            spawnee.position = spawnAnchor.position;
            spawnee.rotation = spawnAnchor.rotation;
        }

        public void MoveToSpawnAnchor(GameObject spawnee)
        {
            spawnee.transform.position = spawnAnchor.position;
            spawnee.transform.rotation = spawnAnchor.rotation;
        }
    }

    protected GI_CustomGameInstance customGameInstance;
    protected bool hasStarted;

    public bool HasStarted => hasStarted;

    protected override IEnumerator Start()
    {
        customGameInstance = GetGameInstance<GI_CustomGameInstance>();
        customGameInstance.LoadGame<SaveData>("The_Sandbox");

        PreInitialization();

        yield return base.Start();
            
        PostInitialization();
    }

    protected virtual void PreInitialization() { }
    protected virtual void PostInitialization() { }

    protected override void Update()
    {
        base.Update();
    }
}