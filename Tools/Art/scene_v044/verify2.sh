#!/bin/sh
cd "E:/University/Homework/虚拟现实作业/BoatAttack"; R=Tools/Art/scene_v044/run_locked.sh
for s in FishingV019 FishingV044Walk FishingV036Portal FishingCoastArt; do echo "== $s"; sh $R $s | grep -E "PASS|FAIL|Walked|STALL|failed" | head -14; done
sh $R FishingV044Scene V044_SCENE_OUT=Logs/V044/scene/after | grep PASS
