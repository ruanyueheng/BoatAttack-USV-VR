#!/bin/sh
cd "E:/University/Homework/虚拟现实作业/BoatAttack"; R=Tools/Art/scene_v044/run_locked.sh
sh $R FishingV044SceneArt | grep PASS
for s in FishingV019 FishingV044Walk FishingV036Portal FishingCoastArt FishingLivingCoast FishingV044Perf; do echo "== $s"; out=$(sh $R $s); echo "$out" | grep -E "PASS|Walked|STALL.*(ShoreRock|Fern|Flower|Meadow|Dune|Pine|Palm|Mangrove)|PERF" | head -14; log=$(echo "$out" | grep -o 'usv-validation-[0-9a-f]*\.log' | head -1); [ -n "$log" ] && grep -m2 -E "FAIL" "Logs/$log"; done
sh $R FishingV044Scene V044_SCENE_OUT=Logs/V044/scene/after | grep PASS
echo ALLDONE
