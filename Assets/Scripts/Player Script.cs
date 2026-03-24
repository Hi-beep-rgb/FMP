using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController))]
public class PlayerScript : MonoBehaviour
{
    private Vector2 input;
    private CharacterController controller;
    private Vector3 direction;

    //Setting the gravity value
    private float gravity = -9.81f;
    [SerializeField] private float gravityMultiplier = 3f;
    private float velocity;

    [SerializeField] private float smoothTime = 0.05f;
    private float currentVelocity;

    [SerializeField] private float speed;

    [SerializeField] private float jumpPower;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        ApplyGravity();
        ApplyRotation();
        ApplyMovement();

        /*if (input.sqrMagnitude == 0) return;

        var targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg; //Rad2Deg = Radience to Degrees
        var angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentVelocity, smoothTime);
        transform.rotation = Quaternion.Euler(0.0f, angle, 0.0f);*/

        /*controller.Move(direction * speed * Time.deltaTime);*/
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

        /*velocity += gravity * gravityMultiplier * Time.deltaTime;*/
        direction.y = velocity;
    }

    private void ApplyRotation()
    {
        if (input.sqrMagnitude == 0) return;

        var targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg; //Rad2Deg = Radience to Degrees
        var angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentVelocity, smoothTime);
        transform.rotation = Quaternion.Euler(0.0f, angle, 0f);
    }

    private void ApplyMovement()
    {
        controller.Move(direction * speed * Time.deltaTime);
    }

    public void Move(InputAction.CallbackContext context)
    {
        input = context.ReadValue<Vector2>();
        direction = new Vector3(input.x, 0f, input.y);
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (!IsGrounded()) return;

        velocity += jumpPower;
    }

    private bool IsGrounded() => controller.isGrounded;
}
