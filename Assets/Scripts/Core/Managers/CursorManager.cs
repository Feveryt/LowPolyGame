using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 鼠标光标管理器
/// 职责：锁定/解锁鼠标，点击画面重新锁定
/// 基于 Input System（项目已切换为新输入系统，禁止使用旧 Input 类）
/// </summary>
public class CursorManager : MonoBehaviour
{
    // 场景开始时锁定并隐藏鼠标光标。
    private void Start()
    {
        LockCursor();
    }

    // 每帧检测重新锁定光标的输入。
    private void Update()
    {
        HandleInput();
    }

    /// <summary>
    /// 锁定鼠标到屏幕中心并隐藏
    /// </summary>
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// 解锁鼠标
    /// </summary>
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 设置面板负责 Esc；此处只在点击画面时重新锁定光标。
    /// </summary>
    private void HandleInput()
    {
        // 左键点击重新锁定
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState == CursorLockMode.None)
        {
            LockCursor();
        }
    }
}
