using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class TestScaffoldPawn : UPawn
    {
        [SerializeField] private float speed = 7.5f;
        public float Speed => speed;
        public Transform cameraTarget;
        public UPawn pawnReference;
    }
}
