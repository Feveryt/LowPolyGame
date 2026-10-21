var sb = new System.Text.StringBuilder();
var outfit = UnityEngine.Object.FindObjectsByType<PlayerOutfit>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)[0];
var pct = outfit.transform;
if (!outfit.IsWeaponDrawn) outfit.ApplyStanceImmediate(true);
UnityEngine.Time.timeScale = 0f;
var animator = outfit.GetComponentInChildren<Animator>();
animator.speed = 0f;
animator.Play("Equipped Locomotion", 0, 0.25f);
animator.Update(0f);

var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var wi = typeof(PlayerOutfit).GetField("currentWeaponInstance", flags).GetValue(outfit) as GameObject;

Transform handR = null; Transform idx1 = null; Transform thumb1 = null; Transform mid1 = null;
var allT = pct.GetComponentsInChildren<Transform>(true);
for (int i = 0; i < allT.Length; i++) {
  string n = allT[i].name;
  if (n == "Hand_R") handR = allT[i];
  if (n == "IndexFinger_01 1") idx1 = allT[i];
  if (n == "Thumb_01 1") thumb1 = allT[i];
  if (n == "Finger_01 1") mid1 = allT[i];
}

sb.AppendLine("=== 骨骼位置（世界） ===");
if (handR != null) sb.AppendLine("Hand_R      " + handR.position.ToString("F4") + "  lossyScale=" + handR.lossyScale.x.ToString("F5"));
if (idx1 != null) sb.AppendLine("IndexFinger_01 " + idx1.position.ToString("F4"));
if (mid1 != null) sb.AppendLine("Finger_01   " + mid1.position.ToString("F4"));
if (thumb1 != null) sb.AppendLine("Thumb_01    " + thumb1.position.ToString("F4"));

sb.AppendLine();
sb.AppendLine("=== 武器 ===");
sb.AppendLine("localPos=" + wi.transform.localPosition.ToString("F3") + "  localScale=" + wi.transform.localScale.x.ToString("F0"));
sb.AppendLine("pivot 世界 = " + wi.transform.position.ToString("F4"));

bool hasB = false; Bounds total = new Bounds();
foreach (Renderer r in wi.GetComponentsInChildren<Renderer>(true)) { if (!hasB) { total = r.bounds; hasB = true; } else { total.Encapsulate(r.bounds); } }
sb.AppendLine("包围盒 center=" + total.center.ToString("F4") + " size=" + total.size.ToString("F4"));

// 武器本地空间里，沿自身 Y 轴不同位置对应的世界坐标（用来找"握持点"）
float scaleLossy = wi.transform.lossyScale.y;
sb.AppendLine();
sb.AppendLine("=== 武器自身 Y 轴上的采样点（本地 Y -> 世界） lossyY=" + scaleLossy.ToString("F5") + " ===");
float[] ys = new float[] { -0.168f, 0f, 0.1f, 0.2f, 0.3f, 0.4193f, 0.6f, 1.007f };
for (int i = 0; i < ys.Length; i++) {
  Vector3 wp = wi.transform.TransformPoint(new Vector3(0f, ys[i], 0f));
  float d = handR != null ? Vector3.Distance(wp, handR.position) : -1f;
  sb.AppendLine("  localY=" + ys[i].ToString("F3") + " -> " + wp.ToString("F4") + "   距Hand_R=" + d.ToString("F4"));
}

if (idx1 != null && thumb1 != null) {
  Vector3 palmCenter = (idx1.position + thumb1.position) * 0.5f;
  sb.AppendLine();
  sb.AppendLine("手心(食指+拇指中点) = " + palmCenter.ToString("F4"));
  sb.AppendLine("武器 pivot 到手心距离 = " + Vector3.Distance(wi.transform.position, palmCenter).ToString("F4"));
}
return sb.ToString();
