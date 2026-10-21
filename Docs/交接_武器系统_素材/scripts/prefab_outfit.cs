var sb = new System.Text.StringBuilder();
string path = "Assets/Resources/Prefabs/Player/Player.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
if (root == null) return "load failed";

var outfit = root.GetComponent<PlayerOutfit>();
var ctrl = root.GetComponent<PlayerController>();
sb.AppendLine("PlayerOutfit=" + (outfit != null) + " PlayerController=" + (ctrl != null));

Transform sheath = null;
Transform handR = null;
var all = root.GetComponentsInChildren<Transform>(true);
for (int i = 0; i < all.Length; i++) {
  if (all[i].name == "SM_Item_Sword") sheath = all[i];
  if (all[i].name == "Hand_R") handR = all[i];
}
sb.AppendLine("sheath=" + (sheath != null ? sheath.name : "NULL") + " handR=" + (handR != null ? handR.name : "NULL"));

if (outfit != null) {
  var so = new UnityEditor.SerializedObject(outfit);
  var pSheath = so.FindProperty("weaponSheathSocket");
  var pEuler = so.FindProperty("sheathLocalEuler");
  var pPos = so.FindProperty("sheathLocalPosition");
  var pAlign = so.FindProperty("autoAlignSheathToDecoration");
  sb.AppendLine("props: sheath=" + (pSheath != null) + " euler=" + (pEuler != null) + " pos=" + (pPos != null) + " align=" + (pAlign != null));
  if (pSheath != null) pSheath.objectReferenceValue = sheath;
  if (pEuler != null) pEuler.vector3Value = new Vector3(0f, 0f, 180f);
  if (pPos != null) pPos.vector3Value = Vector3.zero;
  if (pAlign != null) pAlign.boolValue = true;
  so.ApplyModifiedPropertiesWithoutUndo();
  sb.AppendLine("outfit configured");
}

if (ctrl != null) {
  var so2 = new UnityEditor.SerializedObject(ctrl);
  var pOutfit = so2.FindProperty("playerOutfit");
  if (pOutfit != null) { pOutfit.objectReferenceValue = outfit; so2.ApplyModifiedPropertiesWithoutUndo(); sb.AppendLine("controller linked to outfit"); }
  else sb.AppendLine("playerOutfit prop not found");
}

UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
UnityEditor.AssetDatabase.SaveAssets();
sb.AppendLine("saved " + path);
return sb.ToString();
