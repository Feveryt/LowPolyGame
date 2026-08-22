using UnityEngine;

/// <summary>
/// UI 面板基类
/// 所有面板都继承此基类，统一管理打开/关闭/返回逻辑
/// </summary>
public abstract class UIPanel : MonoBehaviour
{
    // 面板名称（用于 UIManager 字典索引）
    // 面板层级枚举：Background, Normal, Popup, TopMost
    // 打开面板：Open(object data = null)（带可选传入数据）
    // 关闭面板：Close()
    // 返回上一级：GoBack()
    // 是否动画中（防止快速点击）
    // 播放打开/关闭动画
    // ESC 返回处理
}
