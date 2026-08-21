using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 负责跨场景异步加载与全屏加载过渡。
/// 加载遮罩使用独立持久化 Canvas，避免与来源场景的菜单 UI 和 EventSystem 一同销毁。
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneLoader : MonoBehaviour
{
    // 全局唯一的场景加载器实例。
    private static SceneLoader instance;

    // 模拟进度推进到 90% 所需的非缩放时间。
    [SerializeField, Min(0.1f)] private float simulatedProgressDuration = 0.8f;
    // 加载遮罩淡入耗时。
    [SerializeField, Min(0.05f)] private float fadeInDuration = 0.2f;
    // 新场景激活后的遮罩淡出耗时。
    [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.35f;

    // 控制加载遮罩可见度与射线拦截的组件。
    private CanvasGroup overlayCanvasGroup;
    // 显示模拟加载进度的 UGUI Slider。
    private Slider progressSlider;
    // 显示百分比数值的 UGUI Text。
    private Text progressText;
    // 当前是否已进入不可重复触发的加载流程。
    private bool isLoading;

    /// <summary>当前运行时唯一的场景加载器。</summary>
    public static SceneLoader Instance => instance;
    /// <summary>是否正在执行场景切换过渡。</summary>
    public bool IsLoading => isLoading;

    /// <summary>获取或创建跨场景持久化的加载器。</summary>
    public static SceneLoader GetOrCreate()
    {
        if (instance != null)
            return instance;

        GameObject loaderObject = new GameObject(nameof(SceneLoader));
        return loaderObject.AddComponent<SceneLoader>();
    }

    // 初始化单例并创建独立的加载遮罩。
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateOverlay();
    }

    /// <summary>异步加载目标场景，并播放模拟进度与淡入淡出过渡。</summary>
    public void LoadSceneAsync(string scenePath)
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath))
        {
            Debug.LogError($"[{nameof(SceneLoader)}] Scene '{scenePath}' is not included in Build Settings.", this);
            return;
        }

        StartCoroutine(LoadSceneRoutine(scenePath));
    }

    // 按固定时序执行遮罩淡入、模拟进度、异步激活和遮罩淡出。
    private IEnumerator LoadSceneRoutine(string scenePath)
    {
        isLoading = true;
        SetProgress(0f);
        yield return FadeOverlay(1f, fadeInDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        operation.allowSceneActivation = false;
        float elapsed = 0f;

        while (elapsed < simulatedProgressDuration || operation.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            float simulatedProgress = Mathf.Clamp01(elapsed / simulatedProgressDuration) * 0.9f;
            SetProgress(Mathf.Min(simulatedProgress, 0.9f));
            yield return null;
        }

        SetProgress(1f);
        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;

        yield return null;
        yield return FadeOverlay(0f, fadeOutDuration);
        isLoading = false;
    }

    // 创建由 Unity 内置 UGUI 控件构成的独立加载画面。
    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("Loading Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject overlayObject = CreateUiObject("Overlay", canvasObject.transform);
        StretchToParent(overlayObject.GetComponent<RectTransform>());
        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = Color.black;
        overlayCanvasGroup = overlayObject.AddComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvasGroup.interactable = false;

        Text loadingLabel = CreateText("Loading Label", overlayObject.transform, "正在加载", 28, TextAnchor.MiddleCenter);
        SetCenteredLayout(loadingLabel.rectTransform, new Vector2(0f, 42f), new Vector2(360f, 44f));

        progressSlider = CreateProgressSlider(overlayObject.transform);
        SetCenteredLayout(progressSlider.GetComponent<RectTransform>(), Vector2.zero, new Vector2(420f, 18f));

        progressText = CreateText("Progress Text", overlayObject.transform, "0%", 18, TextAnchor.MiddleCenter);
        SetCenteredLayout(progressText.rectTransform, new Vector2(0f, -38f), new Vector2(140f, 30f));
    }

    // 使用非缩放时间将加载遮罩透明度插值到目标值。
    private IEnumerator FadeOverlay(float targetAlpha, float duration)
    {
        float startAlpha = overlayCanvasGroup.alpha;
        overlayCanvasGroup.blocksRaycasts = targetAlpha > 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        overlayCanvasGroup.alpha = targetAlpha;
        overlayCanvasGroup.blocksRaycasts = targetAlpha > 0f;
    }

    // 同步 Slider 与文本形式的模拟进度。
    private void SetProgress(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        progressSlider.value = clampedValue;
        progressText.text = $"{Mathf.RoundToInt(clampedValue * 100f)}%";
    }

    // 创建基础 UGUI 节点。
    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    // 创建统一样式的内置 Text。
    private static Text CreateText(string objectName, Transform parent, string content, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    // 创建黑底青绿色填充的最小进度条。
    private static Slider CreateProgressSlider(Transform parent)
    {
        GameObject sliderObject = CreateUiObject("Loading Progress", parent);
        Image background = sliderObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.14f, 0.17f, 1f);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;

        GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
        StretchToParent(fillArea.GetComponent<RectTransform>(), 4f);
        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.22f, 0.72f, 0.57f, 1f);
        StretchToParent(fill.GetComponent<RectTransform>());
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.targetGraphic = background;
        return slider;
    }

    // 设置居中的固定尺寸 UI 布局。
    private static void SetCenteredLayout(RectTransform transform, Vector2 position, Vector2 size)
    {
        transform.anchorMin = new Vector2(0.5f, 0.5f);
        transform.anchorMax = new Vector2(0.5f, 0.5f);
        transform.pivot = new Vector2(0.5f, 0.5f);
        transform.anchoredPosition = position;
        transform.sizeDelta = size;
    }

    // 将 UI 节点拉伸铺满父级，并保留指定边距。
    private static void StretchToParent(RectTransform transform, float inset = 0f)
    {
        transform.anchorMin = Vector2.zero;
        transform.anchorMax = Vector2.one;
        transform.offsetMin = Vector2.one * inset;
        transform.offsetMax = Vector2.one * -inset;
    }
}
