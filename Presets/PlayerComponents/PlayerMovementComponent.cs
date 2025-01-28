using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

[System.Serializable]
public class MovementSettings
{
    public float speed;
    public float sprintMultiplier = 1.5f;
    public float jumpForce;
    public int jumpCount = 1;
    public Vector3 InputDir { get; set; }

    public int JumpCounter { get; private set; }
    public bool CanJump => JumpCounter > 0;
    public bool hasJumped;
    public bool waitingForLanding;

    public void ResetJumpCounter()
    {
        JumpCounter = jumpCount;
    }

    public void DecrementJumpCounter()
    {
        JumpCounter--;
    }
}

public class PlayerMovementComponent : PlayerBaseComponent
{
    [SerializeField] private MovementSettings movementSettings;

    private Rigidbody rb;

    private Vector3 prevPosition;
    private float unitSpeed;
    private bool shiftPressed;

    public MovementSettings MoveSettings => movementSettings;
    public float UnitSpeed => unitSpeed;

    public bool isGrounded => Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 0.6f);
    public bool isMoving => movementSettings.InputDir.magnitude > 0.01f && isGrounded;
    public GameObject StandingOn => isGrounded ? Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit) ? hit.collider.gameObject : null : null;

    public override void Initialize(P_PlayerPawn context)
    {
        base.Initialize(context);

        InputPriority.MovePriority = 1;

        rb = context.Rb;
        prevPosition = transform.position;

        movementSettings.ResetJumpCounter();

        initialized = true;
    }

    public override void OnMove(Vector2 inputDir, out bool swallowInput)
    {
        movementSettings.InputDir = new Vector3(inputDir.x, 0f, inputDir.y);
        swallowInput = false;
    }

    public override void OnSpace(bool pressed, out bool swallowInput)
    {
        Jump();
        swallowInput = false;
    }

    public override void UpdateTick(out bool swallowTick)
    {
        swallowTick = false;

        unitSpeed = Vector3.Distance(prevPosition, transform.position) / Time.deltaTime;
    
        if (movementSettings.waitingForLanding)
        {
            if (isGrounded)
            {
                movementSettings.waitingForLanding = false;
                movementSettings.hasJumped = false;

                movementSettings.ResetJumpCounter();
            }
        }

        if (!isGrounded && movementSettings.hasJumped)
        {
            movementSettings.waitingForLanding = true;
        }

        prevPosition = transform.position;
    }

    public override void FixedUpdateTick(out bool swallowTick)
    {
        swallowTick = false;

        if (movementSettings.InputDir.magnitude > 0.01f)
        {
            //move player using rigidbody
            float efficiency = isGrounded ? 1f : 0.5f;
            float speed = shiftPressed ? movementSettings.speed * movementSettings.sprintMultiplier : movementSettings.speed;
            rb.MovePosition(rb.position + transform.TransformDirection(movementSettings.InputDir) * (speed * efficiency) * Time.fixedDeltaTime);
        }
    }

    public override void OnShift(bool pressed, out bool swallowInput)
    {
        swallowInput = false;
        shiftPressed = pressed;
    }

    public void Jump()
    {
        if (!movementSettings.CanJump && isGrounded)
        {
            movementSettings.ResetJumpCounter();
        }

        if (movementSettings.CanJump)
        {
            rb.AddForce(Vector3.up * movementSettings.jumpForce, ForceMode.Impulse);
            movementSettings.DecrementJumpCounter();

            movementSettings.hasJumped = true;
        }
    }
}
