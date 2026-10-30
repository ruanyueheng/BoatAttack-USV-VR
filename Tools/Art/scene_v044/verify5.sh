#!/bin/sh
cd "E:/University/Homework/虚拟现实作业/BoatAttack"; R=Tools/Art/scene_v044/run_locked.sh
L(){ powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-Validation-Lanes.ps1 -Suite $1 2>&1; }
sh $R FishingV044SceneArt | grep -E "PASS|FAIL"
for s in FishingV019 FishingV044Walk FishingV036Portal FishingCoastArt; do (echo "== $s"; L $s | grep -E "PASS|FAIL|Walked|failed" | head -4) > /tmp/v5_$s.txt & done; wait; cat /tmp/v5_*.txt
sh $R FishingV044Scene V044_SCENE_OUT=Logs/V044/scene/after | grep -E "PASS|FAIL"
echo ALLDONE
