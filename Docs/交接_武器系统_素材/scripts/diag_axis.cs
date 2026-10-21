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
Transform handR = null; Transform idx1 = null; Transform thumb1 = null;
var allT = pct.GetComponentsInChildren<Transform>(true);
for (int i = 0; i < allT.Length; i++) {
  if (allT[i].name == "Hand_R") handR = allT[i];
  if (allT[i].name == "IndexFinger_01 1") idx1 = allT[i];
  if (allT[i].name == "Thumb_01 1") thumb1 = allT[i];
}
Vector3 palm = (idx1.position + thumb1.position) * 0.5f;
Vector3 F = pct.forward; Vector3 R = pct.right; Vector3 U = pct.up;

sb.AppendLine("角色 forward=" + F.ToString("F2") + " right=" + R.ToString("F2") + " up=" + U.ToString("F2"));
sb.AppendLine("手心=" + palm.ToString("F4") + "  Hand_R=" + handR.position.ToString("F4"));
sb.AppendLine();
sb.AppendLine("euler(当前)            剑身方向(世界)          相对角色(前/右/上)      剑柄中心距手心   剑尖末端距手心");
Quaternion saved = wi.transform.localRotation;

float[][] combos = new float[][] {
  new float[] { -90f, 90f, 0f },
  new float[] { 0f, 90f, 0f },
  new float[] { 60f, 90f, 0f },
  new float[] { 90f, 90f, 0f },
  new float[] { 135f, 90f, 0f },
  new float[] { 180f, 90f, 0f },
  new float[] { 90f, 0f, 0f },
  new float[] { 90f, 180f, 0f }
};

for (int k = 0; k < combos.Length; k++) {
  wi.transform.localRotation = Quaternion.Euler(combos[k][0], combos[k][1], combos[k][2]);
  Vector3 axis = (wi.transform.TransformPoint(new Vector3(0f, 1f, 0f)) - wi.transform.TransformPoint(Vector3.zero)).normalized;
  Vector3 grip = wi.transform.TransformPoint(new Vector3(0f, 0.1f, 0f));
  Vector3 tip = wi.transform.TransformPoint(new Vector3(0f, 1.0f, 0f));
  string rel = "前" + Vector3.Dot(axis, F).ToString("F2") + " 右" + Vector3.Dot(axis, R).ToString("F2") + " 上" + Vector3.Dot(axis, U).ToString("F2");
  sb.AppendLine("(" + combos[k][0] + "," + combos[k][1] + "," + combos[k][2] + ")".PadRight(20) + axis.ToString("F2").PadRight(24) + rel.PadRight(24) + Vector3.Distance(grip, palm).ToString("F3").PadRight(8) + Vector3.Distance(tip, palm).ToString("F3"));
}

wi.transform.localRotation = saved;
// 顺便算一下：要让剑柄(本地Y=0.1)落到手心，pivot 需要平移多少
wi.transform.localRotation = Quaternion.Euler(135f, 90f, 0f);
Vector3 gripNow = wi.transform.TransformPoint(new Vector3(0f, 0.1f, 0f));
Vector3 delta = palm - gripNow;
sb.AppendLine();
sb.AppendLine("当前(135,90,0)下，把剑柄移到手心需要的世界平移 = " + delta.ToString("F4"));
sb.AppendLine("转成 Hand_R 局部偏移 = " + handR.InverseTransformVector(delta).ToString("F2"));
wi.transform.localRotation = saved;
return sb.ToString();
