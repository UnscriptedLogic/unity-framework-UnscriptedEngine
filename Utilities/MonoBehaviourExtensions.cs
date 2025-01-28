using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MonoBehaviourExtensions
{
    public static Action<bool> OnToggledMouse;
    public static Action<bool> OnToggleInput;

    public static Coroutine DelayedAction(this MonoBehaviour monoBehaviour, Action before, float delay, Action after)
    {
        return monoBehaviour.StartCoroutine(DelayedActionCoroutine(before, delay, after));
    }

    public static Coroutine DelayedByFrameAction(this MonoBehaviour monoBehaviour, Action before, Action after)
    {
        return monoBehaviour.StartCoroutine(DelayedByFrameActionCoroutine(before, after));
    }

    private static IEnumerator DelayedActionCoroutine(Action before, float delay, Action after)
    {
        before?.Invoke();
        yield return new WaitForSeconds(delay);
        after?.Invoke();
    }

    private static IEnumerator DelayedByFrameActionCoroutine(Action before, Action after)
    {
        before?.Invoke();
        yield return new WaitForEndOfFrame();
        after?.Invoke();
    }

    public static bool IsPointerOverUI(this MonoBehaviour monoBehaviour)
    {
        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    public static void ToggleMouse(this MonoBehaviour monoBehaviour, bool on)
    {
        Cursor.visible = on;
        Cursor.lockState = on ? CursorLockMode.None : CursorLockMode.Locked;

        OnToggledMouse?.Invoke(on);
    }

    public static void ToggleInput(this MonoBehaviour monoBehaviour, bool on)
    {
        OnToggleInput?.Invoke(on);
    }

    public static void SaveData(this MonoBehaviour monoBehaviour)
    {
        GI_CustomGameInstance gameInstance = UnityEngine.Object.FindAnyObjectByType<GI_CustomGameInstance>();
        gameInstance.SaveGame();
    }

    public static T GetGloballyLoadedSaveData<T>(this MonoBehaviour monoBehaviour) where T : SaveData
    {
        GI_CustomGameInstance gameInstance = UnityEngine.Object.FindAnyObjectByType<GI_CustomGameInstance>();
        return (T)gameInstance.SaveData;
    }

    public static Vector3 SnapToGrid(this MonoBehaviour monoBehaviour, Vector3 position, float cellSize, Vector3 offset)
    {
        return new Vector3(
            Mathf.Round((position.x - offset.x) / cellSize) * cellSize + offset.x,
            Mathf.Round((position.y - offset.y) / cellSize) * cellSize + offset.y,
            Mathf.Round((position.z - offset.z) / cellSize) * cellSize + offset.z
        );
    }
}

public static class TransfromExtensions
{
    public static void ClearChildren(this Transform transform, Predicate<Transform> condition, bool sendToObjectPooler = false)
    {
        foreach (Transform child in transform)
        {
            if (condition != null && !condition(child)) continue;

            if (sendToObjectPooler)
            {
                ObjectPooler.DespawnObject(child.gameObject);
            }
            else
            {
                GameObject.Destroy(child.gameObject);
            }
        }
    }
}

public static class RigidbodyExtensions
{
    /// <summary>
    /// Returns a set of connected Rigidbody objects to the root object using bounds intersection and distance threshold.
    /// </summary>
    public static HashSet<Rigidbody> GetConnectedObjectsByBounds(this Rigidbody rootObject, float connectionThreshold, Predicate<Rigidbody> condition = null, int depth = -1)
    {
        HashSet<Rigidbody> connectedObjects = new HashSet<Rigidbody>();
        Queue<Rigidbody> toProcess = new Queue<Rigidbody>();
        toProcess.Enqueue(rootObject);

        while (toProcess.Count > 0)
        {
            Rigidbody current = toProcess.Dequeue();
            if (connectedObjects.Contains(current)) continue;

            if (condition != null && !condition(current)) continue;

            connectedObjects.Add(current);

            if (depth != -1)
            {
                if (connectedObjects.Count >= depth)
                {
                    break;
                }
            }

            // Get current object's bounds
            Collider currentCollider = current.GetComponent<Collider>();
            if (currentCollider == null) continue;

            Bounds currentBounds = currentCollider.bounds;

            // Expand bounds slightly to account for the connection threshold
            Vector3 expandedSize = currentBounds.size + Vector3.one * connectionThreshold;
            Vector3 center = currentBounds.center;

            // Find nearby colliders using Physics.OverlapBox
            Collider[] nearbyColliders = Physics.OverlapBox(center, expandedSize / 2);

            foreach (Collider nearbyCollider in nearbyColliders)
            {
                Rigidbody nearbyRb = nearbyCollider.attachedRigidbody;
                if (nearbyRb == null || connectedObjects.Contains(nearbyRb) || nearbyRb == current) continue;

                // Check if the bounds overlap or are within the connection threshold
                Bounds nearbyBounds = nearbyCollider.bounds;
                if (currentBounds.Intersects(nearbyBounds) ||
                    (currentBounds.SqrDistance(nearbyBounds.min) <= connectionThreshold * connectionThreshold) ||
                    (currentBounds.SqrDistance(nearbyBounds.max) <= connectionThreshold * connectionThreshold))
                {
                    toProcess.Enqueue(nearbyRb);
                }
            }
        }

        return connectedObjects;
    }
}

public static class Vector3Extensions
{
    public static Vector3 Round(this Vector3 vector, int decimals)
    {
        return new Vector3((float)Math.Round(vector.x, decimals), (float)Math.Round(vector.y, decimals), (float)Math.Round(vector.z, decimals));
    }
}