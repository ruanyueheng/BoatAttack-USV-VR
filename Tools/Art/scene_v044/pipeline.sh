#!/bin/sh
# before capture with original models -> swap in new models -> install materials -> after capture -> walk audit
cd "E:/University/Homework/虚拟现实作业/BoatAttack"; R=Tools/Art/scene_v044/run_locked.sh
sh $R FishingV044Scene V044_SCENE_OUT=Logs/V044/scene/before || exit 1
cp /tmp/v044new/*.fbx Assets/Resources/FishingV035/
sh $R FishingV044SceneArt || exit 2
sh $R FishingV044Scene V044_SCENE_OUT=Logs/V044/scene/after || exit 3
