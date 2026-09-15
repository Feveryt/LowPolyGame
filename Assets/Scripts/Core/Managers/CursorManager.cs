using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 鼠标光标管理器
/// 职责：锁定/解锁鼠标，并在游戏态持续维持锁定
/// 基于 Input System（项目已切换为新输入系统，禁止使用旧 Input 类）
/// </summary>
public class CursorManager : MonoBehaviour
{
    // UI 面板接管鼠标期间阻止游戏态逻辑重新锁定鼠标。
    private bool uiCursorActive;

    /// <summary>当前鼠标是否由背包、设置等 UI 面板接管。</summary>
    public bool IsUiCursorActive => uiCursorActive;

    // 场景开始时锁定并隐藏鼠标光标。
    private void OnEnable()
    {
        if (!uiCursorActive)
            LockCursor();
    }

    // 切回游戏窗口时主动恢复游戏态光标。
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && !uiCursorActive)
            LockCursor();
    }

    // 每帧检测重新锁定光标的输入。
    private void Update()
    {
        HandleRelockInput();
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
    /// 设置 UI 是否接管鼠标；接管时显示鼠标，释放接管时恢复游戏态锁定。
    /// </summary>
    public void SetUiCursorActive(bool active)
    {
        uiCursorActive = active;
        if (uiCursorActive)
            UnlockCursor();
        else
            LockCursor();
    }

    /// <summary>
/// 游戏态下仅在点击非 UI 区域时重新锁定鼠标，避免覆盖 UI 面板的鼠标状态。
/// </summary>
private void HandleRelockInput()
{
    if (uiCursorActive || Cursor.lockState == CursorLockMode.Locked)
        return;

    if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        return;

    if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
        LockCursor();
}
}
