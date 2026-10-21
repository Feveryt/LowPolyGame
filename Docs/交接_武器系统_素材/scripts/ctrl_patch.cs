var sb = new System.Text.StringBuilder();
string ctrlPath = "Assets/ArtRes/Animator/Player.controller";
var ac = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
if (ac == null) return "controller not found";
var sm = ac.layers[0].stateMachine;

UnityEngine.AnimationClip drawClip = null;
UnityEngine.AnimationClip sheatheClip = null;
string[] paths = new string[] {
  "Assets/ArtRes/Dynamic Sword Animset/Animations/InPlace/Uneqip_To_Eqip.FBX",
  "Assets/ArtRes/Dynamic Sword Animset/Animations/InPlace/Eqip_To_Uneqip.FBX" };
for (int i = 0; i < paths.Length; i++) {
    var objs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(paths[i]);
    for (int j = 0; j < objs.Length; j++) {
        var c = objs[j] as UnityEngine.AnimationClip;
        if (c == null) continue;
        if (c.name.StartsWith("__preview__")) continue;
        if (i == 0) drawClip = c; else sheatheClip = c;
    }
}
sb.AppendLine("drawClip=" + (drawClip != null ? drawClip.name + "/" + drawClip.length.ToString("F3") : "null"));
sb.AppendLine("sheatheClip=" + (sheatheClip != null ? sheatheClip.name + "/" + sheatheClip.length.ToString("F3") : "null"));

UnityEditor.Animations.AnimatorState drawState = null;
UnityEditor.Animations.AnimatorState sheatheState = null;
UnityEditor.Animations.AnimatorState unequipLoco = null;
UnityEditor.Animations.AnimatorState equipLoco = null;
UnityEditor.Animations.AnimatorState runUneqip = null;
UnityEditor.Animations.AnimatorState runEqip = null;
for (int i = 0; i < sm.states.Length; i++) {
    var st = sm.states[i].state;
    if (st.name == "Draw Weapon") drawState = st;
    if (st.name == "Eqip_To_Uneqip") sheatheState = st;
    if (st.name == "Unequipped Locomotion") unequipLoco = st;
    if (st.name == "Equipped Locomotion") equipLoco = st;
    if (st.name == "Run_Uneqip") runUneqip = st;
    if (st.name == "Run_Eqip") runEqip = st;
}
sb.AppendLine("found draw=" + (drawState != null) + " sheathe=" + (sheatheState != null) + " unequipLoco=" + (unequipLoco != null) + " equipLoco=" + (equipLoco != null) + " runUneqip=" + (runUneqip != null) + " runEqip=" + (runEqip != null));

if (drawState == null) { drawState = sm.AddState("Draw Weapon", new Vector3(680, 280, 0)); sb.AppendLine("created Draw Weapon"); }
drawState.motion = drawClip;
drawState.writeDefaultValues = true;
if (sheatheState != null && sheatheClip != null) sheatheState.motion = sheatheClip;

for (int i = unequipLoco.transitions.Length - 1; i >= 0; i--) {
    var tr = unequipLoco.transitions[i];
    if (tr.destinationState == equipLoco) { unequipLoco.RemoveTransition(tr); sb.AppendLine("removed Unequipped->Equipped direct"); }
}
for (int i = equipLoco.transitions.Length - 1; i >= 0; i--) {
    var tr = equipLoco.transitions[i];
    if (tr.destinationState == unequipLoco) { equipLoco.RemoveTransition(tr); sb.AppendLine("removed Equipped->Unequipped direct"); }
}

var t1 = unequipLoco.AddTransition(drawState);
t1.hasExitTime = false; t1.exitTime = 0f; t1.duration = 0.08f; t1.hasFixedDuration = true;
t1.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "IsEquipped");

var t2 = drawState.AddTransition(equipLoco);
t2.hasExitTime = true; t2.exitTime = 0.92f; t2.duration = 0.1f; t2.hasFixedDuration = true;

var t3 = equipLoco.AddTransition(sheatheState);
t3.hasExitTime = false; t3.exitTime = 0f; t3.duration = 0.08f; t3.hasFixedDuration = true;
t3.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "IsEquipped");

var t4 = sheatheState.AddTransition(unequipLoco);
t4.hasExitTime = true; t4.exitTime = 0.92f; t4.duration = 0.1f; t4.hasFixedDuration = true;

int addedRun = 0;
if (runUneqip != null) {
    var r1 = runUneqip.AddTransition(drawState);
    r1.hasExitTime = false; r1.exitTime = 0f; r1.duration = 0.08f; r1.hasFixedDuration = true;
    r1.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "IsEquipped");
    addedRun++;
}
if (runEqip != null) {
    var r2 = runEqip.AddTransition(sheatheState);
    r2.hasExitTime = false; r2.exitTime = 0f; r2.duration = 0.08f; r2.hasFixedDuration = true;
    r2.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "IsEquipped");
    addedRun++;
}

sb.AppendLine("transitions added: draw=" + unequipLoco.transitions.Length + " equipLoco=" + equipLoco.transitions.Length + " run=" + addedRun);
UnityEditor.EditorUtility.SetDirty(ac);
UnityEditor.AssetDatabase.SaveAssets();
sb.AppendLine("saved " + ctrlPath);
return sb.ToString();
