using UnityEngine;

/// <summary>
/// UI 系统管理器（单例）
/// 职责：管理所有 UI 面板的打开/关闭/层级，HUD 更新，弹窗提示
/// </summary>
public class UIManager : MonoBehaviour
{
    // Canvas 引用（主 Canvas / 世界空间 Canvas）
    // UI 面板栈（用于返回上一级面板）
    // 面板注册字典：Dictionary<string, UIPanel> panelDict

    // 打开面板：OpenPanel<T>(string panelName) where T : UIPanel
    // 关闭面板：ClosePanel(string panelName)
    // 返回上一面板：GoBack()
    // 显示提示：ShowTip(string message, float duration)
    // 显示确认框：ShowConfirm(string title, string content, Action onConfirm)
    // 显示加载画面：ShowLoading(bool show)

    // HUD 引用：血条、蓝条、小地图、当前技能、任务追踪
    // 更新 HUD：UpdateHUD(PlayerStats stats)
    // 伤害数字生成（世界坐标转屏幕坐标）
}
