using UnityEngine;

/// <summary>
/// 简单的角色移动控制器，基于 CharacterController
/// 配合 Cinemachine FreeLook 实现自由视角
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
        LockCursor();
    }

    private void Update()
    {
        HandleCursorLock();
        HandleMovement();
        ApplyGravity();
    }

    private void LateUpdate()
    {
        // 角色始终面朝相机前方，摄像机始终在角色身后
        Vector3 camForward = Vector3.Scale(mainCam.forward, new Vector3(1, 0, 1)).normalized;

        if (camForward != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(camForward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 处理水平移动（相对相机方向）
    /// </summary>
    private void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 取相机的前方和右方，忽略 Y 轴
        Vector3 camForward = Vector3.Scale(mainCam.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight = Vector3.Scale(mainCam.right, new Vector3(1, 0, 1)).normalized;

        Vector3 moveDir = camRight * horizontal + camForward * vertical;
        controller.SimpleMove(moveDir.normalized * moveSpeed);
    }

    /// <summary>
    /// 处理重力
    /// </summary>
    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// 锁定鼠标到屏幕中心
    /// </summary>
    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// 按 Esc 解锁鼠标
    /// </summary>
    private void HandleCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 鼠标左键点击画面时重新锁定
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            LockCursor();
        }
    }
}