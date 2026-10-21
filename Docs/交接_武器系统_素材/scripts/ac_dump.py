import re, sys

def parse(path):
    text = open(path, encoding="utf-8", errors="replace").read()
    docs = re.split(r"^--- ", text, flags=re.M)
    objs = {}
    for d in docs:
        m = re.match(r"!u!(\d+) &(-?\d+)", d)
        if not m: continue
        objs[m.group(2)] = (m.group(1), d.split("\n",1)[1] if "\n" in d else "")
    return objs

def f(body, name, default=None):
    m = re.search(r"^\s*%s:\s*(.*)$" % re.escape(name), body, flags=re.M)
    return m.group(1).strip() if m else default

def fid(s):
    if not s: return None
    m = re.search(r"fileID: (-?\d+)", s)
    return m.group(1) if m else None

path = sys.argv[1]
objs = parse(path)
tag = sys.argv[2] if len(sys.argv) > 2 else "all"

if tag in ("all","state"):
    print("===== ANIMATOR STATES =====")
    for fid_, (cls, body) in objs.items():
        if cls != "1102": continue
        name = f(body,"m_Name")
        motion = f(body,"m_Motion")
        print("STATE %-28s motion=%s" % (name, motion))
        for t in re.findall(r"- \{fileID: (-?\d+)\}", body.split("m_Transitions:")[1].split("m_StateMachineBehaviours")[0]) if "m_Transitions:" in body else []:
            tb = objs.get(t)
            if not tb: continue
            c, b2 = tb
            conds = re.findall(r"m_ConditionMode: (-?\d+)\s*\n\s*m_ConditionEvent: (\S+)", b2)
            dst = fid(f(b2,"m_DstState"))
            dstname = f(objs[dst][1],"m_Name") if dst in objs else ("EXIT" if f(b2,"m_IsExit")=="1" else "?")
            hasExit = f(b2,"m_HasExitTime"); ex = f(b2,"m_ExitTime"); dur = f(b2,"m_TransitionDuration")
            print("      -> %-24s cond=%s exitTime=%s(%s) dur=%s" % (dstname, conds, hasExit, ex, dur))

if tag in ("all","sm"):
    print("===== STATE MACHINES =====")
    for fid_, (cls, body) in objs.items():
        if cls != "1107": continue
        name = f(body,"m_Name")
        kids = re.findall(r"- \{fileID: (-?\d+)\}", body.split("m_ChildStates:")[1].split("m_ChildStateMachines")[0]) if "m_ChildStates:" in body else []
        kn = [f(objs[k][1],"m_Name") for k in kids if k in objs]
        dfs = fid(f(body,"m_DefaultState"))
        dfn = f(objs[dfs][1],"m_Name") if dfs and dfs in objs else "?"
        anyt = re.findall(r"- \{fileID: (-?\d+)\}", body.split("m_AnyStateTransitions:")[1].split("m_EntryTransitions")[0]) if "m_AnyStateTransitions:" in body else []
        print("SM %-24s default=%-22s children=%s" % (name, dfn, kn))
        for t in anyt:
            if t not in objs: continue
            b2 = objs[t][1]
            conds = re.findall(r"m_ConditionMode: (-?\d+)\s*\n\s*m_ConditionEvent: (\S+)", b2)
            dst = fid(f(b2,"m_DstState")); dstname = f(objs[dst][1],"m_Name") if dst in objs else "?"
            print("      ANY -> %-22s cond=%s dur=%s" % (dstname, conds, f(b2,"m_TransitionDuration")))

if tag in ("all","param"):
    print("===== PARAMETERS =====")
    for fid_, (cls, body) in objs.items():
        if cls != "114" or "m_AnimatorParameters" not in body: continue
        for nm, ty in re.findall(r"m_Name: (\S+)\s*\n\s*m_Type: (-?\d+)", body):
            tmap={"1":"Float","3":"Int","4":"Bool","9":"Trigger"}
            print("   %-22s %s" % (nm, tmap.get(ty,ty)))
