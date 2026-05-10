using UnityEngine;

/// <summary>
/// 角色移动控制器，基于 CharacterController
/// 配合 Cinemachine FreeLook 实现自由视角
/// 依赖：CursorManager（鼠标锁定）、InputManager（输入，后续接入）
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController controller;
    private Transform mainCam;
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        mainCam = Camera.main.transform;
    }

    private void Update()
    {
        HandleMovement();
        ApplyGravity();
    }

    private void LateUpdate()
    {
        RotateTowardCamera();
    }

    /// <summary>
    /// 水平移动（相对相机方向）
    /// </summary>
    private void HandleMovement()
    {
        // 后续可替换为 InputManager.Instance.GetMoveInput()
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 camForward = Vector3.Scale(mainCam.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight = Vector3.Scale(mainCam.right, new Vector3(1, 0, 1)).normalized;

        Vector3 moveDir = camRight * horizontal + camForward * vertical;
        controller.SimpleMove(moveDir.normalized * moveSpeed);
    }

    /// <summary>
    /// 角色始终面朝相机前方
    /// </summary>
    private void RotateTowardCamera()
    {
        Vector3 camForward = Vector3.Scale(mainCam.forward, new Vector3(1, 0, 1)).normalized;

        if (camForward != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(camForward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 重力模拟
    /// </summary>
    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
