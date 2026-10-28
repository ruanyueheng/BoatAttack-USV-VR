"""Compile Assembly-CSharp(+Editor) with Unity's own Roslyn in ~seconds, without opening Unity.

Reuses Unity's last response files (references/defines) but swaps in the *current* .cs files,
so new/renamed scripts are covered. Writes only to a temp dir; never touches Library outputs.
Usage: python Tools/Check-FastCompile.py      -> exit 0 if both assemblies compile, prints errors otherwise.
shortcut: assumes no new asmdef folders since Unity last compiled; rerun Unity once if one is added.
"""
import glob, os, re, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSC = r"C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Data\DotNetSdkRoslyn\csc.dll"
DOTNET = r"C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Data\NetCoreRuntime\dotnet.exe"


def latest(name):
    files = glob.glob(os.path.join(ROOT, "Library/Bee/artifacts/*/" + name + ".rsp"))
    if not files:
        sys.exit("No Unity response file for " + name + "; open/batch-run Unity once.")
    return max(files, key=os.path.getmtime)


def sources(old, editor):
    # Current files in the same folders Unity compiled before (keeps asmdef boundaries intact).
    dirs = {os.path.dirname(s) for s in old}
    found = set()
    for d in dirs:
        for f in glob.glob(os.path.join(ROOT, d, "*.cs")):
            rel = os.path.relpath(f, ROOT).replace("\\", "/")
            if ("/Editor/" in "/" + rel) == editor:
                found.add(rel)
    return sorted(found)


def compile_one(name, editor, out_dir, extra_refs=()):
    lines = open(latest(name), encoding="utf-8").read().splitlines()
    old = [l.strip('"') for l in lines if l.strip('"').endswith(".cs")]
    keep = [l for l in lines if not l.strip('"').endswith(".cs")
            and not l.startswith(("-out:", "-refout:", "-doc:"))
            and not (extra_refs and "Assembly-CSharp.dll" in l and l.startswith("-r:"))]
    dll = os.path.join(out_dir, name + ".dll")
    keep += ['-out:"%s"' % dll] + ['-r:"%s"' % r for r in extra_refs]
    keep += ['"%s"' % s for s in sources(old, editor)]
    rsp = os.path.join(out_dir, name + ".rsp")
    open(rsp, "w", encoding="utf-8").write("\n".join(keep))
    r = subprocess.run([DOTNET, "exec", CSC, "/noconfig", "@" + rsp], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    errors = [l for l in r.stdout.splitlines() if re.search(r"\berror CS\d+", l)]
    return dll, errors


def main():
    out = tempfile.mkdtemp(prefix="fastcompile-")
    dll, errors = compile_one("Assembly-CSharp", False, out)
    if not errors:
        _, errors = compile_one("Assembly-CSharp-Editor", True, out, [dll])
    for e in errors[:40]:
        print(e)
    print("FAST-COMPILE " + ("PASS" if not errors else "FAIL (%d errors)" % len(errors)))
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
