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
    /*[SerializeField] private float smoothTime;*/

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
        cameraRotation.Yaw += input.x * mouseSensitivty.horizontal * BoolToInt(mouseSensitivty.invertHorizontal) * Time.deltaTime;
        cameraRotation.Pitch += input.y * mouseSensitivty.vertical * BoolToInt(mouseSensitivty.invertVertical) * Time.deltaTime;
        cameraRotation.Pitch = Mathf.Clamp(cameraRotation.Pitch, cameraAngle.min, cameraAngle.max);
    }

    private void LateUpdate()
    {
        transform.eulerAngles = new Vector3(cameraRotation.Pitch, cameraRotation.Yaw, 0f);
        transform.position = target.position - transform.forward * distanceToPlayer;
    }

    private static int BoolToInt(bool b) => b ? 1 : -1;
}

[Serializable]
public struct MouseSensitivity
{
    public float horizontal;
    public float vertical;
    public bool invertVertical;
    public bool invertHorizontal;
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

