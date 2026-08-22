using TMPro;
using UnityEngine;

/// <summary>
/// 场景内静态搭建的石质 NPC 交互提示条，向交互系统提供全局显示入口。
/// </summary>
public sealed class DialoguePromptUI : MonoBehaviour
{
    // 当前全局交互提示实例。
    private static DialoguePromptUI instance;
    // 显示按键与 NPC 名称的 TMP 文本。
    [SerializeField] private TMP_Text promptText;
    // 控制提示条可见性和射线阻挡的画布组。
    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>全局 NPC 交互提示实例。</summary>
    public static DialoguePromptUI Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<DialoguePromptUI>(FindObjectsInactive.Include);

            return instance;
        }
    }

    // 注册静态场景提示条并确保其跨场景保持唯一。
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        Hide();
    }

    // 清理销毁对象留下的静态实例引用。
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>显示当前可交互 NPC 的提示文本。</summary>
    public void Show(string message)
    {
        if (promptText != null)
            promptText.text = message;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    /// <summary>隐藏 NPC 交互提示。</summary>
    public void Hide()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
}
