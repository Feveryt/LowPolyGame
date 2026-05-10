using UnityEngine;

/// <summary>
/// 鼠标光标管理器
/// 职责：锁定/解锁鼠标，Esc 解锁、点击画面重新锁定
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
    /// Esc 解锁，左键点击重新锁定
    /// </summary>
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            LockCursor();
        }
    }
}
