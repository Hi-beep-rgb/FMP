using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController))]
public class PlayerScript : MonoBehaviour
{
    #region Variables: Movement

    private Vector2 input;
    private CharacterController controller;
    private Vector3 direction;
    public Animator anim;

    //Setting the gravity value
    private float gravity = -9.81f;
    [SerializeField] private float gravityMultiplier = 3f;
    private float velocity;

    [SerializeField] private float rotationSpeed = 500f;
    private Camera mainCamera;

    [SerializeField] private float speed;

    [SerializeField] private Movement movement;

    [SerializeField] private float jumpPower;

    #endregion

    public EnemyHealth enemyHealth;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        ApplyRotation();
        ApplyGravity();
        ApplyMovement();
    }

    private void ApplyGravity()
    {
        if(IsGrounded() && velocity < 0f)
        {
            velocity = -1f;
        }
        else
        {
            velocity += gravity * gravityMultiplier * Time.deltaTime;
        }

        direction.y = velocity;
    }

    private void ApplyRotation()
    {
        if (input.sqrMagnitude == 0) return;

        direction = Quaternion.Euler(0f, mainCamera.transform.eulerAngles.y, 0f) * new Vector3(input.x, 0f, input.y);
        var targetRotation = Quaternion.LookRotation(direction, Vector3.up);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void ApplyMovement()
    {
        var targetSpeed = movement.isSprinting ? movement.speed * movement.multiplier : movement.speed;
        movement.currentSpeed = Mathf.MoveTowards(movement.currentSpeed, targetSpeed, movement.acceleration * Time.deltaTime);

        controller.Move(direction * movement.currentSpeed * Time.deltaTime);
    }

    public void Move(InputAction.CallbackContext context)
    {
        input = context.ReadValue<Vector2>();
        direction = new Vector3(input.x, 0f, input.y);

        //Plays the walking animation
        if(direction.sqrMagnitude > 0.1f || direction.sqrMagnitude < -0.1f)
        {
            anim.SetBool("isWalking", true);
            /*InvokeRepeating(nameof(PlayFootsteps), 0f, 0.8f);*/
        }
        else
        {
            anim.SetBool("isWalking", false);
            /*CancelInvoke(nameof(PlayFootsteps));*/
        }
    }

    /*void PlayFootsteps()
    {
        AudioManager.instance.Play("Footstep");
    }*/

    /*public void Jump(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (!IsGrounded()) return;

        velocity += jumpPower;
    }*/

    public void Sprint(InputAction.CallbackContext context)
    {
        if(movement.isSprinting = context.started || context.performed)
        {
            anim.SetBool("isRunning", true);
        }
        else
        {
            anim.SetBool("isRunning", false);
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        anim.SetTrigger("punch");
        enemyHealth.TakeDamage(5);
    }
    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        anim.SetTrigger("swing");
        enemyHealth.TakeDamage(15);
    }

    private bool IsGrounded() => controller.isGrounded;
}

[Serializable]
public struct Movement
{
    public float speed;
    public float multiplier;
    public float acceleration;

    [HideInInspector] public bool isSprinting;
    [HideInInspector] public float currentSpeed;
}