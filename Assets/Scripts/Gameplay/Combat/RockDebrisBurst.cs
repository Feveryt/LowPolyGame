using UnityEngine;

/// <summary>
/// 碎石飞溅表现：在指定点抛出若干块石头/砖块模型（纯视觉，无碰撞与判定），
/// 带重力与随机自转，落地反弹一次后静止并收缩消失。
/// 由攻击命中逻辑通过静态入口 Burst 触发，整组用完即销毁。
/// </summary>
[DisallowMultipleComponent]
public sealed class RockDebrisBurst : MonoBehaviour
{
    // 碎石水平初速范围，单位为米每秒。
    [SerializeField] private Vector2 speedRange = new Vector2(3f, 6f);
    // 碎石垂直初速，单位为米每秒。
    [SerializeField, Min(0f)] private float upSpeed = 4f;
    // 碎石缩放范围。
    [SerializeField] private Vector2 scaleRange = new Vector2(0.15f, 0.35f);
    // 重力加速度，单位为米每二次方秒。
    [SerializeField] private float gravity = -18f;
    // 整组碎石的兜底存活时长，单位为秒。
    [SerializeField, Min(0.3f)] private float lifetime = 2.2f;

    /// <summary>
    /// 在指定点爆出一组碎石。
    /// scaleMultiplier 用于整体缩放（例如小型震波命中用 0.8，跳砸用 1.2）。
    /// </summary>
    public static void Burst(GameObject[] prefabs, Vector3 center, int count, float scaleMultiplier = 1f)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0)
            return;

        GameObject host = new GameObject("RockDebrisBurst");
        host.transform.position = center;
        host.AddComponent<RockDebrisBurst>().Initialize(prefabs, count, scaleMultiplier);
        Object.Destroy(host, 3f);
    }

    // 生成所有碎石块。
    private void Initialize(GameObject[] prefabs, int count, float scaleMultiplier)
    {
        for (int index = 0; index < count; index++)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null)
                continue;

            GameObject chunk = Instantiate(prefab, transform);
            chunk.transform.position = Center + new Vector3(
                Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 0.6f), Random.Range(-0.3f, 0.3f));
            float scale = Random.Range(scaleRange.x, scaleRange.y) * scaleMultiplier;
            chunk.transform.localScale = Vector3.one * scale;
            chunk.AddComponent<DebrisChunk>().Initialize(this);
        }

        Destroy(gameObject, lifetime + 0.5f);
    }

    // 发射中心（Burst 时把宿主放在该点）。
    private Vector3 Center => transform.position;

    // 单块碎石的飞行动力学与落地表现。
    private sealed class DebrisChunk : MonoBehaviour
    {
        private Vector3 velocity;
        private Vector3 angularVelocity;
        private float gravity;
        private float bouncesLeft = 1;
        private float settleTimer = -1f;
        private Vector3 initialScale;

        /// <summary>从宿主组件读取运动参数并赋予随机初速。</summary>
        public void Initialize(RockDebrisBurst owner)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            float speed = Random.Range(owner.speedRange.x, owner.speedRange.y);
            velocity = new Vector3(direction.x, 0f, direction.y) * speed + Vector3.up * owner.upSpeed;
            angularVelocity = new Vector3(
                Random.Range(-540f, 540f),
                Random.Range(-540f, 540f),
                Random.Range(-540f, 540f));
            gravity = owner.gravity;
            initialScale = transform.localScale;
        }

        // 每帧积分运动；落地反弹一次后静止收缩。
        private void Update()
        {
            if (settleTimer >= 0f)
            {
                settleTimer += Time.deltaTime;
                float remain = 1f - Mathf.Clamp01(settleTimer / 0.4f);
                transform.localScale = initialScale * remain;
                if (settleTimer >= 0.4f)
                    Destroy(gameObject);
                return;
            }

            velocity.y += gravity * Time.deltaTime;
            Vector3 position = transform.position + velocity * Time.deltaTime;

            if (velocity.y < 0f && Physics.Raycast(
                position + Vector3.up * 0.5f,
                Vector3.down,
                out RaycastHit hit,
                1.2f,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                if (bouncesLeft > 0)
                {
                    bouncesLeft--;
                    velocity = new Vector3(velocity.x * 0.5f, Mathf.Abs(velocity.y) * 0.35f, velocity.z * 0.5f);
                    if (velocity.magnitude < 1f)
                        BeginSettle();
                }
                else
                {
                    BeginSettle();
                }
            }

            transform.position = position;
            transform.Rotate(angularVelocity * Time.deltaTime, Space.Self);
        }

        // 进入静止收缩阶段。
        private void BeginSettle()
        {
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            settleTimer = 0f;
        }
    }
}
