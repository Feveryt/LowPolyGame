using UnityEngine;

/// <summary>
/// 为跨场景常驻 UI 提供唯一实例和生命周期管理的通用面板基类。
/// </summary>
public abstract class SingletonPanel<T> : UIPanel where T : SingletonPanel<T>
{
    /// <summary>当前运行时唯一的面板实例。</summary>
    public static T Instance { get; private set; }

    /// <summary>
    /// 注册当前面板为唯一实例；重复场景实例会在初始化时自行销毁。
    /// </summary>
    protected bool TryInitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        Instance = (T)this;
        DontDestroyOnLoad(gameObject);
        return true;
    }

    /// <summary>
    /// 在子类销毁时清理静态实例引用。
    /// </summary>
    protected void ReleaseSingleton()
    {
        if (Instance == this)
            Instance = null;
    }
}
