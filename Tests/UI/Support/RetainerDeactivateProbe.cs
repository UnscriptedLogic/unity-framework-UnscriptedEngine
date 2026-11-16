using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class RetainerDeactivateProbe : MonoBehaviour
    {
        public GameObject Target;
        private void OnDisable()
        {
            if (Target != null) Target.SetActive(false);
        }
    }
}
