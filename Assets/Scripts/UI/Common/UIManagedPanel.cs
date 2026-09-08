using UnityEngine;

/// <summary>
/// UIManager 使用的运行时面板标记，保存稳定 ID 以便识别 inactive 的已有实例。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIManagedPanel : MonoBehaviour
{
    [SerializeField] private string panelId;

    /// <summary>该实例对应的 UIManager 配置 ID。</summary>
    public string PanelId => panelId;

    /// <summary>由 UIManager 写入配置 ID。</summary>
    public void SetPanelId(string id)
    {
        panelId = id;
    }
}
