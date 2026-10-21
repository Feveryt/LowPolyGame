"""通用 Unity MCP 客户端：以 stdio 方式拉起 mcp-for-unity 服务端并调用单个工具。

用法:
    python mcp_call.py <tool_name> '<json_args>'
    python mcp_call.py --list
"""
import json, os, subprocess, sys, threading, time

UVX = r"C:\Users\29540\.local\bin\uvx.exe"
CMD = [UVX, "--from", "mcpforunityserver==10.2.0", "mcp-for-unity"]
LOGPATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mcp_call.log")

log_file = open(LOGPATH, "w", encoding="utf-8", buffering=1)


def log(*a):
    log_file.write(" ".join(str(x) for x in a) + "\n")


env = dict(os.environ)
env["PYTHONUNBUFFERED"] = "1"

proc = subprocess.Popen(CMD, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                        stderr=subprocess.PIPE, text=True, encoding="utf-8",
                        errors="replace", bufsize=1, env=env)

responses = {}
resp_lock = threading.Lock()


def reader():
    try:
        for line in proc.stdout:
            line = line.strip()
            if not line:
                continue
            try:
                obj = json.loads(line)
            except Exception:
                log("NON-JSON:", line[:400])
                continue
            if "id" in obj:
                with resp_lock:
                    responses[obj["id"]] = obj
            else:
                log("NOTIFY:", json.dumps(obj, ensure_ascii=False)[:400])
    except Exception as e:
        log("READER STOPPED:", repr(e))


threading.Thread(target=reader, daemon=True).start()


def send(obj):
    proc.stdin.write(json.dumps(obj) + "\n")
    proc.stdin.flush()


def wait_for(req_id, timeout):
    deadline = time.time() + timeout
    while time.time() < deadline:
        with resp_lock:
            if req_id in responses:
                return responses.pop(req_id)
        time.sleep(0.25)
    return None


def kill_tree():
    try:
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                       capture_output=True, timeout=15)
    except Exception:
        pass
    try:
        proc.kill()
    except Exception:
        pass


def main():
    args = sys.argv[1:]
    if not args:
        print("usage: mcp_call.py <tool> '<json>' | --list")
        return 2

    send({"jsonrpc": "2.0", "id": 1, "method": "initialize",
          "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                     "clientInfo": {"name": "wb-probe", "version": "1.0"}}})
    init = wait_for(1, 120)
    if init is None:
        print("!! initialize 超时（服务端未就绪）")
        log("initialize timeout")
        return 3
    log("initialized:", json.dumps(init.get("result", {}).get("serverInfo", {}), ensure_ascii=False))

    send({"jsonrpc": "2.0", "method": "notifications/initialized"})
    time.sleep(0.5)

    if args[0] == "--list":
        send({"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}})
        r = wait_for(2, 60)
        if r:
            names = [t["name"] for t in r.get("result", {}).get("tools", [])]
            print("TOOLS(%d): %s" % (len(names), ", ".join(names)))
        else:
            print("!! tools/list 超时")
        return 0

    if args[0] == "--schema":
        send({"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}})
        r = wait_for(2, 60)
        if r is None:
            print("!! tools/list 超时")
            return 3
        want = args[1] if len(args) > 1 else None
        for t in r.get("result", {}).get("tools", []):
            if want and t["name"] != want:
                continue
            print("### " + t["name"])
            print("desc:", (t.get("description") or "")[:600])
            print("schema:", json.dumps(t.get("inputSchema", {}), ensure_ascii=False)[:2500])
            print()
        return 0

    tool = args[0]
    params = json.loads(args[1]) if len(args) > 1 else {}
    log("CALL", tool, json.dumps(params, ensure_ascii=False)[:500])
    send({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
          "params": {"name": tool, "arguments": params}})
    r = wait_for(3, 180)
    if r is None:
        print("!! 工具调用超时: %s" % tool)
        log("tool timeout")
        return 4

    if "error" in r:
        print("ERROR:", json.dumps(r["error"], ensure_ascii=False)[:3000])
        return 5

    result = r.get("result", {})
    if result.get("isError"):
        print("TOOL RETURNED ERROR")
    img_index = 0
    for item in result.get("content", []):
        itype = item.get("type")
        if itype == "text":
            print(item.get("text", ""))
        elif itype == "image":
            import base64
            ext = "png"
            mt = item.get("mimeType", "")
            if "jpeg" in mt or "jpg" in mt:
                ext = "jpg"
            out_dir = os.path.dirname(os.path.abspath(__file__))
            path = os.path.join(out_dir, "mcp_shot_%d.%s" % (img_index, ext))
            with open(path, "wb") as f:
                f.write(base64.b64decode(item.get("data", "")))
            print("[image saved] %s (%s)" % (path, mt))
            log("image saved:", path, mt)
            img_index += 1
        else:
            print("[%s content]" % itype)
    return 0


try:
    code = main()
finally:
    kill_tree()
    try:
        err = proc.stderr.read()
    except Exception:
        err = ""
    if err:
        tail = [ln for ln in err.splitlines() if ln.strip()][-8:]
        log("STDERR TAIL:", " | ".join(tail)[:1200])
    log("DONE")
    log_file.close()
    sys.stdout.flush()
    os._exit(code)
