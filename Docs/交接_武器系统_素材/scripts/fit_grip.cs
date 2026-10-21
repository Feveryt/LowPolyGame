var sb = new System.Text.StringBuilder();
var outfit = UnityEngine.Object.FindObjectsByType<PlayerOutfit>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)[0];
var pct = outfit.transform;
if (!outfit.IsWeaponDrawn) outfit.ApplyStanceImmediate(true);
UnityEngine.Time.timeScale = 0f;
var animator = outfit.GetComponentInChildren<Animator>();
animator.speed = 0f;
animator.Play("Equipped Locomotion", 0, 0.25f);
animator.Update(0f);

var catalog = UnityEngine.Resources.Load<ItemDefinitionCatalog>("ItemDefinitionCatalog");
if (catalog == null) return "no catalog";

var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var fWeap = typeof(PlayerOutfit).GetField("currentWeaponInstance", flags);
Transform handR = null; Transform idx1 = null; Transform thumb1 = null;
var allT = pct.GetComponentsInChildren<Transform>(true);
for (int i = 0; i < allT.Length; i++) {
  if (allT[i].name == "Hand_R") handR = allT[i];
  if (allT[i].name == "IndexFinger_01 1") idx1 = allT[i];
  if (allT[i].name == "Thumb_01 1") thumb1 = allT[i];
}
Vector3 palmLocal = handR.InverseTransformPoint((idx1.position + thumb1.position) * 0.5f);
Vector3 gripLocal = new Vector3(0f, 0.1f, 0f);
sb.AppendLine("手心局部坐标=" + palmLocal.ToString("F4"));
sb.AppendLine();
sb.AppendLine("id  名称        prefab尺寸(Y)      拟合 localPosition           剑柄末端偏差");

int[] ids = new int[] { 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008 };
for (int k = 0; k < ids.Length; k++) {
  ItemDefinition item = null;
  if (!catalog.TryGetItem(ids[k], out item) || item == null || !item.IsEquippable) continue;
  outfit.Equip(item);
  var wi = fWeap.GetValue(outfit) as GameObject;
  if (wi == null) { sb.AppendLine(ids[k] + " 未能装备"); continue; }

  wi.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
  wi.transform.localPosition = Vector3.zero;
  Vector3 gripZero = wi.transform.TransformPoint(gripLocal);
  Vector3 pivotTarget = handR.TransformPoint(palmLocal) - (gripZero - wi.transform.position);
  wi.transform.position = pivotTarget;
  Vector3 fitted = wi.transform.localPosition;

  Vector3 axis = (wi.transform.TransformPoint(new Vector3(0f, 1f, 0f)) - wi.transform.position).normalized;
  Vector3 F = pct.forward; Vector3 R = pct.right; Vector3 U = pct.up;

  bool hasB = false; Bounds bb = new Bounds();
  foreach (Renderer r in wi.GetComponentsInChildren<Renderer>(true)) { if (!hasB) { bb = r.bounds; hasB = true; } else { bb.Encapsulate(r.bounds); } }
  float gripErr = Vector3.Distance(wi.transform.TransformPoint(gripLocal), handR.TransformPoint(palmLocal));

  sb.AppendLine(ids[k] + " " + item.DisplayName.PadRight(10) + bb.size.y.ToString("F2").PadRight(16)
    + "(" + fitted.x.ToString("F2") + ", " + fitted.y.ToString("F2") + ", " + fitted.z.ToString("F2") + ")".PadRight(30)
    + " 前" + Vector3.Dot(axis, F).ToString("F2") + " 右" + Vector3.Dot(axis, R).ToString("F2") + " 上" + Vector3.Dot(axis, U).ToString("F2")
    + "  误差" + gripErr.ToString("F4"));
}
UnityEngine.Time.timeScale = 1f;
return sb.ToString();
