"""Copy the exact downloaded Go modules' root license/notice texts into a package."""
import json
from pathlib import Path
import shutil
import subprocess
import sys

destination = Path(sys.argv[1]).resolve() / "licenses" / "go"
destination.mkdir(parents=True, exist_ok=True)
subprocess.run(["go", "mod", "download", "all"], check=True)
data = subprocess.check_output(["go", "list", "-m", "-json", "all"], text=True)
decoder = json.JSONDecoder()
modules = []
while data.strip():
    module, end = decoder.raw_decode(data.lstrip())
    data = data.lstrip()[end:]
    if module.get("Main"):
        continue
    root = Path(module["Dir"])
    files = [path for path in root.iterdir() if path.is_file() and path.name.upper().startswith(("LICENSE", "COPYING", "NOTICE", "PATENTS"))]
    if not files:
        raise SystemExit(f"No upstream license found: {module['Path']}")
    target = destination / (module["Path"].replace("/", "_") + "@" + module["Version"])
    target.mkdir(parents=True, exist_ok=True)
    for file in files:
        shutil.copyfile(file, target / file.name)
    modules.append({"path": module["Path"], "version": module["Version"], "licenses": [file.name for file in files]})
goroot = Path(subprocess.check_output(["go", "env", "GOROOT"], text=True).strip())
shutil.copyfile(goroot / "LICENSE", destination / "Go-LICENSE")
(destination / "modules.json").write_text(json.dumps(modules, indent=2) + "\n", encoding="utf-8")
print(f"Copied original license/notice texts for {len(modules)} pinned Go modules and the Go runtime.")
