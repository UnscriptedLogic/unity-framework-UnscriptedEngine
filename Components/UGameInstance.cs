using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine;

public class UGameInstance : UObject
{
    public static UGameInstance singleton { get; private set; } = null;

    protected virtual void Awake()
    {
        if (singleton != null)
        {
            Debug.Log($"Found duplicate GameInstance on {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        singleton = this;

        DontDestroyOnLoad(gameObject);
    }
}
