#!/bin/sh
cd "E:/University/Homework/虚拟现实作业/BoatAttack"; R=Tools/Art/scene_v044/run_locked.sh
sh $R FishingV044SceneArt | grep -E "PASS|FAIL"
for s in FishingV019 FishingV036Portal FishingV044Walk FishingCoastArt; do echo "== $s"; out=$(sh $R $s); echo "$out" | grep -E "PASS|Walked" | head -3; log=$(echo "$out" | grep -o 'usv-validation-[0-9a-f]*\.log' | head -1); [ -n "$log" ] && grep -m1 FAIL "Logs/$log"; done
sh $R FishingV044Scene V044_SCENE_OUT=Logs/V044/scene/after | grep -E "PASS|FAIL"
echo ALLDONE
