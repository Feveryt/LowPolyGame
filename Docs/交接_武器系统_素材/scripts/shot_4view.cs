var sb = new System.Text.StringBuilder();
UnityEngine.Time.timeScale = 1f;
var players = UnityEngine.Object.FindObjectsByType<PlayerOutfit>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
if (players.Length == 0) return "no PlayerOutfit";
var pct = players[0].transform;

var camGO = new GameObject("TmpShotCam");
var cam = camGO.AddComponent<UnityEngine.Camera>();
var main = UnityEngine.Camera.main;
if (main != null) cam.CopyFrom(main);
cam.enabled = false;
cam.fieldOfView = 42f;

Vector3 fwd = pct.forward; fwd.y = 0f; fwd.Normalize();
Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
Vector3 focus = pct.position + Vector3.up * 1.15f;
Vector3[] dirs = new Vector3[] { fwd, -fwd, right, -right };
string[] tags = new string[] { "FRONT", "BACK", "RIGHT", "LEFT" };

int W = 400; int H = 620;
int cols = 4; int rows = 1; int total = 4;
var big = new UnityEngine.Texture2D(W * cols, H * rows, UnityEngine.TextureFormat.RGB24, false);
var rt = new UnityEngine.RenderTexture(W, H, 24);
cam.targetTexture = rt;

for (int i = 0; i < total; i++) {
  cam.transform.position = focus + dirs[i] * 3.1f + Vector3.up * 0.15f;
  cam.transform.LookAt(focus);
  cam.Render();
  UnityEngine.RenderTexture.active = rt;
  var small = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
  small.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0);
  small.Apply();
  big.SetPixels(i * W, 0, W, H, small.GetPixels());
  UnityEngine.Object.DestroyImmediate(small);
  sb.AppendLine("slot " + i + " = " + tags[i]);
}

big.Apply();
UnityEngine.RenderTexture.active = null;
cam.targetTexture = null;
string outName = "stance_sheet";
var opt = UnityEngine.Object.FindObjectsByType<PlayerOutfit>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)[0];
if (!opt.IsWeaponDrawn) outName = "stance_sheathed";
else outName = "stance_drawn";
System.IO.File.WriteAllBytes("C:/Users/29540/AppData/Local/Temp/" + outName + ".png", big.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(rt);
UnityEngine.Object.DestroyImmediate(camGO);
sb.AppendLine("saved " + outName + " drawn=" + opt.IsWeaponDrawn);
return sb.ToString();
