#!/bin/sh
cd "E:/University/Homework/虚拟现实作业/BoatAttack"; R=Tools/Art/scene_v044/run_locked.sh
[ "$SKIP_INSTALL" ] || sh $R FishingV044SceneArt || exit 2
sh $R FishingV044Scene V044_SCENE_OUT=Logs/V044/scene/after || exit 3
for s in FishingV044Walk FishingV019 FishingV036Portal FishingLivingCoast FishingCoastArt; do echo "== $s"; sh $R $s | grep -E "PASS|FAIL|Walked|STALL|failed" | head -14; done
