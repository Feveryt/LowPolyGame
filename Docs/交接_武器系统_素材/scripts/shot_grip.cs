var sb = new System.Text.StringBuilder();
var outfit = UnityEngine.Object.FindObjectsByType<PlayerOutfit>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)[0];
var pct = outfit.transform;
if (!outfit.IsWeaponDrawn) outfit.ApplyStanceImmediate(true);
UnityEngine.Time.timeScale = 0f;

var animator = outfit.GetComponentInChildren<Animator>();
if (animator == null) return "no animator";
animator.speed = 0f;

var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var wi = typeof(PlayerOutfit).GetField("currentWeaponInstance", flags).GetValue(outfit) as GameObject;
if (wi == null) return "no weapon";
Transform handR = null;
var allT = pct.GetComponentsInChildren<Transform>(true);
for (int i = 0; i < allT.Length; i++) { if (allT[i].name == "Hand_R") handR = allT[i]; }
if (handR == null) return "no Hand_R";

sb.AppendLine("weapon localPos=" + wi.transform.localPosition.ToString("F2") + " localRot=" + wi.transform.localRotation.eulerAngles.ToString("F1") + " scale=" + wi.transform.localScale.x.ToString("F0"));
sb.AppendLine("weapon parent=" + wi.transform.parent.name);

var camGO = new GameObject("TmpShotCam");
var cam = camGO.AddComponent<UnityEngine.Camera>();
var main = UnityEngine.Camera.main;
if (main != null) cam.CopyFrom(main);
cam.enabled = false;
cam.fieldOfView = 42f;

string[] states = new string[] { "Equipped Locomotion", "Attack_02", "Attack_02", "Attack_02", "Attack_02", "Attack_01", "Attack_16", "Attack_06" };
float[] times = new float[] { 0.25f, 0.15f, 0.35f, 0.55f, 0.8f, 0.45f, 0.5f, 0.45f };
int W = 300; int H = 400;
int cols = 4; int rows = 2;
var big = new UnityEngine.Texture2D(W * cols, H * rows, UnityEngine.TextureFormat.RGB24, false);
var rt = new UnityEngine.RenderTexture(W, H, 24);
cam.targetTexture = rt;

for (int k = 0; k < states.Length; k++) {
  animator.Play(states[k], 0, times[k]);
  animator.Update(0f);
  UnityEngine.Camera cam2 = cam;
  Vector3 handPos = handR.position;
  Vector3 fwd = pct.forward; fwd.y = 0f; fwd.Normalize();
  Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
  cam2.transform.position = handPos + right * 2.1f + Vector3.up * 0.15f - fwd * 0.25f;
  cam2.transform.LookAt(handPos);
  cam2.Render();
  UnityEngine.RenderTexture.active = rt;
  var small = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
  small.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0);
  small.Apply();
  int cx = k % cols; int cy = rows - 1 - (k / cols);
  big.SetPixels(cx * W, cy * H, W, H, small.GetPixels());
  UnityEngine.Object.DestroyImmediate(small);
  sb.AppendLine("slot " + k + " = " + states[k] + " @" + times[k]);
}

big.Apply();
UnityEngine.RenderTexture.active = null;
cam.targetTexture = null;
System.IO.File.WriteAllBytes("C:/Users/29540/AppData/Local/Temp/hand_grip2.png", big.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(rt);
UnityEngine.Object.DestroyImmediate(camGO);
UnityEngine.Time.timeScale = 1f;
return sb.ToString();
