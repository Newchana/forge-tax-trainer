import UnityPy, os, json, sys
root = r"E:\ForgeTax v26.10.12\ForgeTax_Data"
found = []
for dirpath, _, files in os.walk(root):
    for f in files:
        p = os.path.join(dirpath, f)
        try:
            env = UnityPy.load(p)
        except Exception:
            continue
        for obj in env.objects:
            if obj.type.name == "TextAsset":
                try:
                    d = obj.read()
                except Exception:
                    continue
                name = getattr(d, "m_Name", "") or getattr(d, "name", "")
                found.append((p, name))
                if "conomy" in str(name):
                    data = d.m_Script if hasattr(d, "m_Script") else d.script
                    if isinstance(data, bytes):
                        data = data.decode("utf-8", "replace")
                    out = os.path.join(r"D:\AiProject\forgetaxcheat", "economy_dump.json")
                    open(out, "w", encoding="utf-8").write(data)
                    print("SAVED", out, len(data))
print("TextAssets found:", found[:40])
