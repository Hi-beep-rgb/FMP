using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System;
using UnityEngine.Rendering;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Transform target;
    private float distanceToPlayer;
    private Vector2 input;
    [SerializeField] private MouseSensitivity mouseSensitivty;
    [SerializeField] private CameraAngle cameraAngle;
    private CameraRotation cameraRotation;
    /*[SerializeField] private float smoothTime;
    private Vector3 currentVelocity = Vector3.zero;*/

    private void Awake()
    {
        distanceToPlayer = Vector3.Distance(transform.position, target.position);
    }

    public void Look(InputAction.CallbackContext context)
    {
        input = context.ReadValue<Vector2>();
    }

    private void Update()
    {
        cameraRotation.Yaw += input.x * mouseSensitivty.horizontal * Time.deltaTime;
        cameraRotation.Pitch += input.y * mouseSensitivty.vertical * Time.deltaTime;
        cameraRotation.Pitch = Mathf.Clamp(cameraRotation.Pitch, cameraAngle.min, cameraAngle.max);
    }

    private void LateUpdate()
    {
        transform.eulerAngles = new Vector3(cameraRotation.Pitch, cameraRotation.Yaw, 0f);
        transform.position = target.position - transform.forward * distanceToPlayer;
    }
}

[Serializable]
public struct MouseSensitivity
{
    public float horizontal;
    public float vertical;
}

public struct CameraRotation
{
    public float Pitch;
    public float Yaw;
}

[Serializable]
public struct CameraAngle
{
    public float min;
    public float max;
}

