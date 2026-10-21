var sb = new System.Text.StringBuilder();
UnityEngine.Time.timeScale = 1f;
var outfit = UnityEngine.Object.FindObjectsByType<PlayerOutfit>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)[0];
var pct = outfit.transform;
var catalog = UnityEngine.Resources.Load<ItemDefinitionCatalog>("ItemDefinitionCatalog");
if (catalog == null) return "no catalog";
if (outfit.IsWeaponDrawn) outfit.ApplyStanceImmediate(false);

var camGO = new GameObject("TmpShotCam");
var cam = camGO.AddComponent<UnityEngine.Camera>();
var main = UnityEngine.Camera.main;
if (main != null) cam.CopyFrom(main);
cam.enabled = false;
cam.fieldOfView = 38f;
Vector3 fwd = pct.forward; fwd.y = 0f; fwd.Normalize();
Vector3 focus = pct.position + Vector3.up * 1.05f;
cam.transform.position = focus - fwd * 3.0f + Vector3.up * 0.1f;
cam.transform.LookAt(focus);

int[] ids = new int[] { 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007 };
int W = 300; int H = 470; int cols = 4; int rows = 2;
var big = new UnityEngine.Texture2D(W * cols, H * rows, UnityEngine.TextureFormat.RGB24, false);
var rt = new UnityEngine.RenderTexture(W, H, 24);
cam.targetTexture = rt;

for (int k = 0; k < ids.Length; k++) {
  ItemDefinition item = null;
  if (!catalog.TryGetItem(ids[k], out item) || item == null || !item.IsEquippable) { sb.AppendLine(ids[k] + " 不可用"); continue; }
  outfit.Equip(item);
  outfit.ApplyStanceImmediate(false);

  var wi = outfit.GetComponentInChildren<UnityEngine.Renderer>(true);
  Bounds wb = new Bounds();
  var rends = outfit.transform.Find("Root").GetComponentsInChildren<UnityEngine.Renderer>(true);
  bool hb = false;
  for (int r = 0; r < rends.Length; r++) {
    if (rends[r].name.StartsWith("SM_Wep")) {
      if (!hb) { wb = rends[r].bounds; hb = true; } else { wb.Encapsulate(rends[r].bounds); }
    }
  }
  sb.AppendLine(ids[k] + " " + item.DisplayName + "  腰高=" + (hb ? (wb.center.y - pct.position.y).ToString("F2") : "-") + "m 长=" + (hb ? wb.size.y.ToString("F2") : "-"));

  cam.Render();
  UnityEngine.RenderTexture.active = rt;
  var small = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
  small.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0);
  small.Apply();
  int cx = k % cols; int cy = rows - 1 - (k / cols);
  big.SetPixels(cx * W, cy * H, W, H, small.GetPixels());
  UnityEngine.Object.DestroyImmediate(small);
}

big.Apply();
UnityEngine.RenderTexture.active = null;
cam.targetTexture = null;
System.IO.File.WriteAllBytes("C:/Users/29540/AppData/Local/Temp/weapon_gallery.png", big.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(rt);
UnityEngine.Object.DestroyImmediate(camGO);
sb.AppendLine("saved weapon_gallery.png");
return sb.ToString();
