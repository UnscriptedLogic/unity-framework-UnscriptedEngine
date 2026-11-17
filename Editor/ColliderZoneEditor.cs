using UnityEditor;
using UnityEngine;
using UnscriptedEngine.Configuration;
using UnscriptedEngine.Gameplay;

namespace UnscriptedEngine.Editor
{
    [CustomEditor(typeof(ColliderZone))]
    [CanEditMultipleObjects]
    public sealed class ColliderZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Enable Configure Wand"))
                    foreach (Object selected in targets)
                    {
                        ColliderZone zone = (ColliderZone)selected;
                        if (zone.GetComponent<ConfigurableObject>() == null)
                            Undo.AddComponent<ConfigurableObject>(zone.gameObject);
                    }
            }
            foreach (Object selected in targets)
            {
                ColliderZone zone = (ColliderZone)selected;
                Collider[] colliders = zone.GetComponents<Collider>();
                if (colliders.Length != 1)
                    EditorGUILayout.HelpBox("Use exactly one local Collider per zone. Put compound shapes on separate child zones.", MessageType.Warning);
                else if (colliders[0].attachedRigidbody == null)
                    EditorGUILayout.HelpBox("Physics callbacks need a Rigidbody on at least one side. Solid collisions require a dynamic body.", MessageType.Info);
                if (colliders.Length == 1 && !colliders[0].isTrigger)
                    EditorGUILayout.HelpBox("Framework CharacterMovementComponent forwards controller hits automatically. Other controller movers must call ColliderZone.ForwardControllerHit(hit) from OnControllerColliderHit. Kinematic/static pairs do not send ordinary Rigidbody collision callbacks.", MessageType.Info);
            }
        }

        private void OnSceneGUI()
        {
            ColliderZone zone = (ColliderZone)target;
            if (!zone.ShowGizmos || zone.Shape == null) return;
            Handles.Label(zone.Shape.bounds.center, zone.name + (zone.Shape.isTrigger ? " [Trigger]" : " [Collision]")
                + (Application.isPlaying ? "\nContacts: " + zone.ContactCount : string.Empty));
        }
    }
}
