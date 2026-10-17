using UnityEngine;
namespace UnscriptedEngine
{
    public static class CameraRelativeMovement
    {
        public static void Apply(CharacterMovementComponent movement, Transform body, Transform view, Vector2 input,
           bool bodyRelative, bool sprinting, float walkSpeed, float sprintScale, float rotationSpeed, float deltaTime)
        {
            movement.MaxWalkSpeed = walkSpeed * (sprinting ? sprintScale : 1f);

            Transform yawSource = bodyRelative
                ? body
                : view;

            Vector3 flatForward = Vector3.ProjectOnPlane(yawSource.forward, Vector3.up);

            if (flatForward.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion controlRotation = Quaternion.LookRotation(flatForward, Vector3.up);
                movement.SetControlYaw(controlRotation.eulerAngles.y);

                if ((!bodyRelative) && input != Vector2.zero)
                {
                    Vector3 movementDirection = controlRotation * new Vector3(input.x, 0f, input.y);
                    Quaternion targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);

                    body.rotation = Quaternion.RotateTowards(
                        body.rotation,
                        targetRotation,
                        rotationSpeed * deltaTime);
                }
            }

            movement.SetMovementInput(input);

        }
    }
}
