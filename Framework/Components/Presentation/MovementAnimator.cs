using UnityEngine;
namespace UnscriptedEngine
{
    public class MovementAnimator : MonoBehaviour
    {
        [SerializeField] private CharacterMovementComponent movementComponent;
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";
        private int speedHash;
        protected virtual void OnEnable()
        {
            speedHash = Animator.StringToHash(speedParameter);
            UpdateManager.RegisterUpdate(this, Animate);
        }
        protected virtual void OnDisable() => UpdateManager.UnregisterUpdate(this);
        private void Animate(float deltaTime)
        {
            if (movementComponent == null || animator == null) return;
            Vector3 velocity = movementComponent.Velocity; velocity.y = 0;
            animator.SetFloat(speedHash, velocity.magnitude / Mathf.Max(0.001f, movementComponent.MaxWalkSpeed));
        }
    }
}
