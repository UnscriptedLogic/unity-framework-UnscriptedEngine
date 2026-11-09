using UnityEngine;

namespace UnscriptedEngine.Dialogue
{
    public sealed class DialogueSpeaker : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("ID used by dialogue lines. Must be unique within a conversation's participants.")]
        private string speakerId;
        [SerializeField]
        [Tooltip("Visible speaker name. Empty uses the GameObject name.")]
        private string displayName;
        [SerializeField]
        [Tooltip("Optional portrait for screen dialogue.")]
        private Sprite portrait;
        [SerializeField]
        [Tooltip("Speech bubble anchor, usually a child above the head. Empty uses this transform.")]
        private Transform headAnchor;

        public string SpeakerId => speakerId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Portrait => portrait;
        public Transform HeadAnchor => headAnchor != null ? headAnchor : transform;

        public void Initialize(string id, string speakerName, Transform anchor = null)
        { speakerId = id; displayName = speakerName; headAnchor = anchor; }
    }
}
