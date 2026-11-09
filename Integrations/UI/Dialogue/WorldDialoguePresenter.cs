using UnityEngine;
using UnscriptedEngine.Dialogue;

namespace UnscriptedEngine.UI.Dialogue
{
    public sealed class WorldDialoguePresenter : DialoguePresenter
    {
        [SerializeField]
        [Tooltip("Viewer camera. Empty resolves Camera.main once when a line is shown.")]
        private Camera viewer;
        [SerializeField]
        [Tooltip("Face the viewer camera while dialogue is visible.")]
        private bool faceCamera = true;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Maximum visible distance from the camera. Zero disables distance culling.")]
        private float maximumDistance = 25f;
        [Header("Debug")]
        [SerializeField]
        [Tooltip("Show the visibility radius while selected.")]
        private bool showGizmos = true;

        private Transform anchor;
        private CanvasGroup visibility;
        public bool HasAnchor => anchor != null;

        public override void Present(DialogueLine line, DialogueSpeaker speaker)
        {
            anchor = speaker != null ? speaker.HeadAnchor : null;
            if (viewer == null) viewer = Camera.main;
            if (visibility == null) visibility = GetComponent<CanvasGroup>();
            base.Present(line, speaker);
            UpdatePlacement();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (anchor != null) UpdatePlacement();
        }

        private void UpdatePlacement()
        {
            if (anchor == null || viewer == null)
            {
                visibility.alpha = 0f;
                visibility.blocksRaycasts = false;
                visibility.interactable = false;
                return;
            }
            transform.position = anchor.position;
            if (faceCamera) transform.rotation = viewer.transform.rotation;
            Vector3 displacement = anchor.position - viewer.transform.position;
            bool visible = Vector3.Dot(viewer.transform.forward, displacement) > 0f &&
                (maximumDistance <= 0f || displacement.sqrMagnitude <= maximumDistance * maximumDistance);
            visibility.alpha = visible ? 1f : 0f;
            visibility.blocksRaycasts = visible;
            visibility.interactable = visible;
        }

        public override void HideDialogue() { anchor = null; base.HideDialogue(); }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || maximumDistance <= 0f) return;
            Gizmos.color = Color.cyan;
            Vector3 center = anchor != null ? anchor.position : transform.position;
            Gizmos.DrawWireSphere(center, maximumDistance);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(center, $"Dialogue visibility: {maximumDistance:0.##} m");
#endif
        }
    }
}
