using UnityEngine;
using UnityEngine.UI;

/// <summary>首次进入 Demo 时显示一次可跳过的全屏剧情文字。</summary>
public sealed class OpeningNarrative : MonoBehaviour
{
    private const string SeenKey = "LowPolyGame.UnsignedGuardian.OpeningSeen";
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text narrativeText;
    [SerializeField] private Button dismissButton;
    [SerializeField, TextArea(3, 6)] private string text;

    /// <summary>配置由启动器生成的开场 UI 引用与固定文本。</summary>
    public void Configure(CanvasGroup group, Text narrative, Button dismiss, string content)
    {
        canvasGroup = group;
        narrativeText = narrative;
        dismissButton = dismiss;
        text = content;
    }

    // 按本地进度决定显示，并连接点击跳过操作。
    private void Awake()
    {
        if (narrativeText != null)
            narrativeText.text = text;
        if (dismissButton != null)
            dismissButton.onClick.AddListener(Dismiss);
        if (PlayerPrefs.GetInt(SeenKey, 0) == 1)
            DismissImmediately();
        else
            Show();
    }

    // 清理按钮监听，避免销毁对象保留回调。
    private void OnDestroy()
    {
        if (dismissButton != null)
            dismissButton.onClick.RemoveListener(Dismiss);
    }

    /// <summary>关闭开场文字并记录已观看状态。</summary>
    public void Dismiss()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        DismissImmediately();
    }

    // 显示非阻塞开场面板。
    private void Show()
    {
        if (canvasGroup == null)
            return;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    // 隐藏面板并停止阻挡场景交互。
    private void DismissImmediately()
    {
        if (canvasGroup == null)
            return;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}
