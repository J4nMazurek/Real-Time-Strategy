using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovementScript : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform PitchObject;//controls the pitch

    [Header("Movement Settings")]
    public float camMovementSpeed;
    public float movementDampeningForce;
    public float sprintMultiplier;
    Vector3 velocity;

    [Header("Rotation Settings")]
    public float camRotationSpeed;
    public float rotationLerpTime;
        
    private float yaw;
    private float yawVelocity;
    private float yawTarget;

    [Header("Zoom Settings")]
    public AnimationCurve zoomToPitch;
    public AnimationCurve zoomToDistance;
    public float zoomSpeed;
    public float zoomLerpTime;

    private float currentZoom;
    private float targetZoom = 0.5f;
    private float zoomVelocity;


    private InputSystem_Actions inputActions;
    private Vector2 controls;
    private Vector2 savedMousePos;
    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        yaw = transform.eulerAngles.y;
        yawTarget = yaw;
    }


    void UpdateMovement()
    {

        controls = inputActions.Player.Move.ReadValue<Vector2>();
    
        Vector3 forward = transform.forward; forward.y = 0; forward.Normalize();
        Vector3 right   = transform.right;   right.y = 0; right.Normalize();
        Vector3 moveDir = forward * controls.y + right * controls.x;

        Vector3 targetVel = moveDir * camMovementSpeed * (inputActions.Player.Sprint.IsPressed() ? sprintMultiplier : 1);
        var accel = (targetVel - velocity) * movementDampeningForce;
        velocity += accel * Time.deltaTime;

        transform.position += velocity * Time.deltaTime;
    }

    void UpdateYawRotation()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            savedMousePos = Mouse.current.position.ReadValue();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        if (Mouse.current.rightButton.isPressed)
        {
            var lookDelta = inputActions.Player.Look.ReadValue<Vector2>();
            yawTarget   += lookDelta.x * camRotationSpeed * Time.deltaTime;
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            Mouse.current.WarpCursorPosition(savedMousePos);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        yaw = Mathf.SmoothDamp(yaw, yawTarget, ref yawVelocity, rotationLerpTime);
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, yaw, transform.rotation.eulerAngles.z);
    }

    void UpdateZoomAndPitch()
    {
        targetZoom += Mouse.current.scroll.ReadValue().y * zoomSpeed;
        targetZoom = Mathf.Clamp01(targetZoom);
        currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, zoomLerpTime);

        float pitch = zoomToPitch.Evaluate(1 - currentZoom);
        PitchObject.transform.rotation = Quaternion.Euler(pitch, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z);

        var position = cam.transform.localPosition;
        cam.transform.localPosition = new Vector3(position.x, position.y, -zoomToDistance.Evaluate(1 - currentZoom));
    }

    void Update()
    {
        UpdateMovement();
        UpdateYawRotation();
        UpdateZoomAndPitch();
    }
}
