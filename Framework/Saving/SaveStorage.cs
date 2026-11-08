using System.IO;
using UnityEngine;

namespace UnscriptedEngine.Saving
{
    public static class SaveStorage
    {
        /// <summary>Create on Unity's main thread. Dispose the returned storage when its owner shuts down.</summary>
        public static ISaveStorage CreateDefault()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WebSaveStorage.Create(Application.companyName + "." + Application.productName + ".saves");
#else
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));
#endif
        }
    }
}
