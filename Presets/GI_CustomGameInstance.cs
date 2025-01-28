using System;
using System.IO;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public float volume;
    public float mouseSens;

    public SaveData()
    {
        volume = 1;
        mouseSens = 100f;
    }
}

public class GI_CustomGameInstance : UGameInstance
{
    [SerializeField] private Texture2D cursorIcon;

    private SaveData saveData;
    private string loadedFolder;

    public SaveData SaveData => saveData;

    protected override void Awake()
    {
        base.Awake();

        Application.wantsToQuit += WantsToQuit;
        //Cursor.SetCursor(cursorIcon, Vector2.zero, CursorMode.Auto);
    }

    public void LoadGame<T>(string folder) where T : SaveData
    {
        loadedFolder = folder;
        if (!File.Exists(Application.persistentDataPath + $"/{loadedFolder}/save.json"))
        {
            Directory.CreateDirectory(Application.persistentDataPath + $"/{loadedFolder}");
            saveData = Activator.CreateInstance<T>();

            Debug.Log("Creating new save...");

            SaveGame();
            return;
        }


        string json = File.ReadAllText(Application.persistentDataPath + $"/{loadedFolder}/save.json");
        saveData = JsonUtility.FromJson<T>(json);
    }

    public void SaveGame()
    {
        if (loadedFolder == string.Empty)
        {
            return;
        }

        string json = JsonUtility.ToJson(saveData);
        File.WriteAllText(Application.persistentDataPath + $"/{loadedFolder}/save.json", json);

        Debug.Log("Game saved!");
    }

    private bool WantsToQuit()
    {
        SaveGame();
        return true;
    }

    public void ClearSave()
    {
        if (loadedFolder == string.Empty)
        {
            return;
        }

        File.Delete(Application.persistentDataPath + $"/{loadedFolder}/save.json");
        saveData = Activator.CreateInstance<SaveData>() as SaveData;
        SaveGame();
    }
}