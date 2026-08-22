using UnityEngine;

/// <summary>
/// 全项目共享的玩家对话展示资料。
/// 资产放在 Resources/Dialogue 下时，运行时管理器会自动加载它。
/// </summary>
[CreateAssetMenu(menuName = "Low Poly Game/Dialogue/Presentation Settings", fileName = "DialoguePresentationSettings")]
public sealed class DialoguePresentationSettings : ScriptableObject
{
    // 玩家在所有对话框中的显示名称。
    [SerializeField] private string playerName = "Player";
    // 玩家发言时使用的头像图片。
    [SerializeField] private Sprite playerPortrait;
    // 玩家头像的固定显示位置。
    [SerializeField] private DialoguePortraitSide playerPortraitSide = DialoguePortraitSide.Right;

    /// <summary>玩家显示名称。</summary>
    public string PlayerName => playerName;
    /// <summary>玩家头像。</summary>
    public Sprite PlayerPortrait => playerPortrait;
    /// <summary>玩家头像显示侧。</summary>
    public DialoguePortraitSide PlayerPortraitSide => playerPortraitSide;
}
