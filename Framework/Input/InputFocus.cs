using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Modal UI leases. Disposing one panel never releases another panel's focus.</summary>
    public static class InputFocus
    {
        private sealed class Lease : IDisposable
        {
            internal UnityEngine.Object Owner;
            internal UController Controller;
            internal bool Global;
            public void Dispose() { leases.Remove(this); ApplyCursor(); }
        }

        private static readonly List<Lease> leases = new();
        private static bool gameplayCursorLocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            leases.Clear();
            gameplayCursorLocked = false;
            ApplyCursor();
        }

        public static IDisposable Acquire(UnityEngine.Object owner, UController controller = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var lease = new Lease { Owner = owner, Controller = controller, Global = controller == null };
            leases.Add(lease);
            ApplyCursor();
            return lease;
        }

        public static bool IsBlocked(UController controller)
        {
            Prune();
            foreach (var lease in leases)
                if (lease.Global || lease.Controller == controller) return true;
            return false;
        }

        public static void SetGameplayCursorLocked(bool locked)
        {
            gameplayCursorLocked = locked;
            ApplyCursor();
        }

        private static void Prune()
        {
            bool changed = false;
            for (int i = leases.Count - 1; i >= 0; i--)
                if (leases[i].Owner == null || (!leases[i].Global && leases[i].Controller == null))
                { leases.RemoveAt(i); changed = true; }
            if (changed) ApplyCursor();
        }

        private static void ApplyCursor()
        {
            bool locked = gameplayCursorLocked && leases.Count == 0;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
