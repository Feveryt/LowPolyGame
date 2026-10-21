import re, sys, os

def load(path):
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        return f.read()

def parse(text):
    docs = re.split(r"^--- ", text, flags=re.M)
    objs = {}
    order = []
    for d in docs:
        m = re.match(r"!u!(\d+) &(\d+)", d)
        if not m:
            continue
        cls, fid = m.group(1), m.group(2)
        body = d.split("\n", 1)[1] if "\n" in d else ""
        objs[fid] = {"cls": cls, "body": body}
        order.append(fid)
    return objs, order

def field(body, name, default=None):
    m = re.search(r"^\s*%s:\s*(.*)$" % re.escape(name), body, flags=re.M)
    return m.group(1).strip() if m else default

def fid_of(s):
    if not s:
        return None
    m = re.search(r"fileID: (-?\d+)", s)
    return m.group(1) if m else None

class Model:
    def __init__(self, path):
        self.path = path
        text = load(path)
        self.objs, self.order = parse(text)
        self.go_name = {}
        self.go_transform = {}
        self.tr_gameobject = {}
        self.tr_father = {}
        self.tr_children = {}
        self.go_comps = {}
        for fid, o in self.objs.items():
            if o["cls"] == "1":
                n = field(o["body"], "m_Name")
                self.go_name[fid] = n if n else "(unnamed)"
                comps = re.findall(r"^\s*- component: \{fileID: (-?\d+)\}", o["body"], flags=re.M)
                self.go_comps[fid] = [c for c in comps if c != "0"]
                for c in comps:
                    if c in self.objs and self.objs[c]["cls"] in ("4", "224"):
                        self.go_transform[fid] = c
            elif o["cls"] in ("4", "224"):
                self.tr_gameobject[fid] = fid_of(field(o["body"], "m_GameObject"))
                self.tr_father[fid] = fid_of(field(o["body"], "m_Father"))
                m = re.search(r"m_Children:\n((?:\s*-\s*\{fileID: -?\d+\}\n?)+)", o["body"])
                self.tr_children[fid] = re.findall(r"fileID: (-?\d+)", m.group(1)) if m else []

    def roots(self):
        out = []
        for fid, o in self.objs.items():
            if o["cls"] == "1":
                t = self.go_transform.get(fid)
                if t is None:
                    out.append(fid)
                elif self.tr_father.get(t) in (None, "0"):
                    out.append(fid)
        return out

    def rect(self, go_fid):
        t = self.go_transform.get(go_fid)
        if t is None:
            return None
        b = self.objs[t]["body"]
        def v(k):
            return field(b, k)
        return dict(anchorMin=v("m_AnchorMin"), anchorMax=v("m_AnchorMax"),
                    anchoredPosition=v("m_AnchoredPosition"), sizeDelta=v("m_SizeDelta"),
                    pivot=v("m_Pivot"), scale=v("m_LocalScale"), rotation=v("m_LocalRotation"))

    def children(self, go_fid):
        t = self.go_transform.get(go_fid)
        if t is None:
            return []
        res = []
        for ct in self.tr_children.get(t, []):
            g = self.tr_gameobject.get(ct)
            if g:
                res.append(g)
        return res

    def comp_summary(self, go_fid):
        out = []
        for c in self.go_comps.get(go_fid, []):
            o = self.objs.get(c)
            if not o:
                continue
            cls = o["cls"]
            if cls in ("4", "224"):
                continue
            if cls == "114":
                g = fid_of(field(o["body"], "m_Script"))
                out.append("Mono")
            else:
                known = {"223": "Canvas", "222": "CanvasRenderer", "225": "CanvasGroup",
                         "65": "BoxCollider", "82": "AudioSource", "212": "SpriteRenderer",
                         "95": "Animator", "198": "ParticleSystem"}
                out.append(known.get(cls, "c" + cls))
        return out

    def active(self, go_fid):
        return field(self.objs[go_fid]["body"], "m_IsActive", "1")

def dump_rects(m, fid, indent=0, out=None):
    n = m.go_name.get(fid, "?")
    r = m.rect(fid)
    act = m.active(fid)
    flag = "" if act == "1" else "  [INACTIVE]"
    comps = ",".join(m.comp_summary(fid))
    if r:
        print("  " * indent + "%-24s anc=%s..%s pos=%s size=%s pv=%s | %s%s" % (
            n, r["anchorMin"], r["anchorMax"], r["anchoredPosition"], r["sizeDelta"], r["pivot"], comps, flag))
    else:
        print("  " * indent + "%-24s [no rect] | %s%s" % (n, comps, flag))
    for ch in m.children(fid):
        dump_rects(m, ch, indent + 1)

def main():
    path = sys.argv[1]
    mode = sys.argv[2] if len(sys.argv) > 2 else "rect"
    m = Model(path)
    print("FILE:", os.path.basename(path), "| objs:", len(m.objs), "| roots:", len(m.roots()))
    if mode == "names":
        for fid in m.order:
            if m.objs[fid]["cls"] == "1":
                print("%-24s comps=%s" % (m.go_name[fid], m.comp_summary(fid)))
    else:
        for r in m.roots():
            dump_rects(m, r, 0)

if __name__ == "__main__":
    main()
