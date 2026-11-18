using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine
{
    /// <summary>Name/ordinal scene addresses for shared authored objects. Keep hierarchy and names stable while in use.</summary>
    public static class SceneObjectAddress
    {
        public static bool TryCreate(Transform target, out string address)
        {
            address = null;
            if (target == null || !target.gameObject.scene.IsValid()) return false;
            Scene scene = target.gameObject.scene;
            List<string> segments = new List<string>();
            for (Transform current = target; current != null; current = current.parent)
            {
                int ordinal = 0;
                if (current.parent != null)
                {
                    foreach (Transform sibling in current.parent)
                    { if (sibling == current) break; if (sibling.name == current.name) ordinal++; }
                }
                else foreach (GameObject root in scene.GetRootGameObjects())
                { if (root.transform == current) break; if (root.name == current.name) ordinal++; }
                segments.Add(Uri.EscapeDataString(current.name) + ":" + ordinal);
            }
            segments.Reverse();
            address = (string.IsNullOrEmpty(scene.path) ? "@" + scene.name : scene.path) + "|" + string.Join("/", segments);
            return true;
        }

        public static Transform Resolve(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            int separator = address.IndexOf('|');
            if (separator < 1) return null;
            string sceneId = address.Substring(0, separator);
            Scene scene = sceneId[0] == '@' ? SceneManager.GetSceneByName(sceneId.Substring(1)) : SceneManager.GetSceneByPath(sceneId);
            if (!scene.IsValid() || !scene.isLoaded) return null;
            string[] segments = address.Substring(separator + 1).Split('/');
            if (segments.Length == 0 || !TryReadSegment(segments[0], out string name, out int ordinal)) return null;
            Transform current = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name && ordinal-- == 0) { current = root.transform; break; }
            for (int index = 1; current != null && index < segments.Length; index++)
            {
                if (!TryReadSegment(segments[index], out name, out ordinal)) return null;
                Transform next = null;
                foreach (Transform child in current)
                    if (child.name == name && ordinal-- == 0) { next = child; break; }
                current = next;
            }
            return current;
        }

        private static bool TryReadSegment(string segment, out string name, out int ordinal)
        {
            name = null; ordinal = -1;
            int separator = segment.LastIndexOf(':');
            if (separator < 0 || !int.TryParse(segment.Substring(separator + 1), out ordinal) || ordinal < 0) return false;
            try { name = Uri.UnescapeDataString(segment.Substring(0, separator)); return true; }
            catch (UriFormatException) { return false; }
        }
    }
}
