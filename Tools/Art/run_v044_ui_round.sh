cd "E:/University/Homework/虚拟现实作业/BoatAttack"
run(){ for i in $(seq 1 40); do out=$(powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-Validation-Lanes.ps1 -Suite $1 2>&1); l=$(echo "$out" | grep -o "Logs.usv-validation-[0-9a-f]*.log" | head -1); if echo "$out" | grep -q "Close the Unity editor" || grep -q "crash has been" "$l" 2>/dev/null || (grep -q "error CS" "$l" 2>/dev/null && ! grep "error CS" "$l" | grep -q "FishingHUD\|FishingWorldSign\|FishingV019Art\|FishingUITheme\|FishingShore\|FishingDockConsole\|FishingUICapture"); then sleep 90; else echo "== $1 $l"; grep -h "\] PASS\|\] FAIL\|error CS" "$l" | sort -u | tail -4; return; fi; done; echo "== $1 gave up"; }
tag=$1
run FishingBuild
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-FishingFeedbackCapture.ps1 -Player Builds/FishingPrototype-v0.43.0/FishingPrototype.exe 2>&1 | tail -1
d=$(ls -td Logs/feedback-* | head -1); rm -rf Logs/V044/ui/$tag; mkdir -p Logs/V044/ui/$tag; cp $d/capture-*/*.png Logs/V044/ui/$tag/
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-FishingUICapture.ps1 -Player Builds/FishingPrototype-v0.43.0/FishingPrototype.exe 2>&1 | tail -2
d=$(ls -td Logs/ui-* | head -1); mkdir -p Logs/V044/ui/$tag/menus; cp $d/capture-*/*.png Logs/V044/ui/$tag/menus/ 2>/dev/null; ls Logs/V044/ui/$tag/menus; grep -h "UI-CAPTURE" $d/player.log | tail -3
