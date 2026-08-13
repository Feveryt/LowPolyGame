using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 鼠标光标管理器
/// 职责：锁定/解锁鼠标，Esc 解锁、点击画面重新锁定
/// 基于 Input System（项目已切换为新输入系统，禁止使用旧 Input 类）
/// </summary>
public class CursorManager : MonoBehaviour
{
    private void Start()
    {
        LockCursor();
    }

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
    /// Esc 解锁，左键点击重新锁定（Input System 版）
    /// </summary>
    private void HandleInput()
    {
        // Esc 解锁
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        // 左键点击重新锁定
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState == CursorLockMode.None)
        {
            LockCursor();
        }
    }
}
