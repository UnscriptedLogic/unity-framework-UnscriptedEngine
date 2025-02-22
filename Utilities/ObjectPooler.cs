using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    public static List<PooledObjectInfo> pooledObjects = new List<PooledObjectInfo>();
    
    public static T SpawnObject<T>(T obj, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        return SpawnObject(obj.gameObject, position, rotation, parent: parent).GetComponent<T>();
    }

    public static GameObject SpawnObject(GameObject obj, Vector3 position, Quaternion rotation, Action<GameObject> OnComplete = null, Transform parent = null)
    {
        PooledObjectInfo pool = pooledObjects.Find(x => x.LookupString == obj.name);

        if (pool == null)
        {
            GameObject poolParent = new GameObject(obj.name + " Pool");

            pool = new PooledObjectInfo();
            pool.LookupString = obj.name;
            pool.parent = poolParent.transform;
            pooledObjects.Add(pool);

        }

        GameObject spawnableObj = pool.InactiveObjects.FirstOrDefault();

        if (spawnableObj != null)
        {
            spawnableObj.transform.position = position;
            spawnableObj.transform.rotation = rotation;
            spawnableObj.transform.SetParent(parent);
            spawnableObj.SetActive(true);
            pool.InactiveObjects.Remove(spawnableObj);
        }
        else
        {
            spawnableObj = Instantiate(obj, position, rotation, parent);
            spawnableObj.name = obj.name;
        }

        OnComplete?.Invoke(spawnableObj);

        return spawnableObj;
    }

    public static void DespawnObject(GameObject obj)
    {
        string objectName = obj.name.Replace("(Clone)", "");

        PooledObjectInfo pool = pooledObjects.Find(x => x.LookupString == objectName);

        if (pool == null)
        {
            pool = new PooledObjectInfo();
            pool.LookupString = obj.name;
            pooledObjects.Add(pool);

            Debug.LogWarning($"Seems like you're trying to despawn a non pooled object: {objectName}. I've created a pool just in case but you need to instantiate it from the pool first.");
        }

        obj.transform.position = Vector3.zero;
        obj.transform.rotation = Quaternion.identity;
        obj.transform.SetParent(pool.parent);
        obj.SetActive(false);
        pool.InactiveObjects.Add(obj);
    }

    public static void DespawnObject(GameObject gameObject, float delay, MonoBehaviour mono)
    {
        mono.DelayedAction(null, delay, () => DespawnObject(gameObject));
    }
}

public class PooledObjectInfo
{
    public string LookupString;
    public List<GameObject> InactiveObjects = new List<GameObject>();
    public Transform parent;
}