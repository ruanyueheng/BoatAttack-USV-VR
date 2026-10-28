using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    /// <summary>
    /// v044 residents: skinned Quaternius characters as Humanoids, URP atlas material, 1024 textures.
    /// Clips: ResidentClips.fbx (UAL takes cut by Tools/Art/build_v044_residents.py), renamed to the brain's short names.
    /// </summary>
    public sealed class FishingV044ResidentImporter : AssetPostprocessor
    {
        private const string Folder="Assets/Resources/FishingV044/Residents/";
        // library take -> name used by FishingResidentBrain
        private static readonly Dictionary<string,string> Used=new Dictionary<string,string>{
            {"Idle_Loop","Idle"},{"Idle_Talking_Loop","Talk"},{"Fixing_Kneeling","Fix"},{"PickUp_Table","PickUp"},{"Interact","Interact"},
            {"Farm_Watering","Water"},{"Farm_PlantSeed","Plant"},{"Farm_Harvest","Harvest"},{"Idle_FoldArms_Loop","FoldArms"},
            {"Idle_Lantern_Loop","Lantern"},{"Yes","Nod"},{"Consume","Drink"},{"Chest_Open","Chest"},{"Walk_Loop","Walk"}};
        private static readonly string[] Loops={"Idle","Talk","Fix","Water","FoldArms","Lantern","Walk"};
        private static readonly Dictionary<string,string> Map=new Dictionary<string,string>{
            {"Hips","pelvis"},{"Spine","spine_01"},{"Chest","spine_02"},{"UpperChest","spine_03"},{"Neck","neck_01"},{"Head","Head"},
            {"LeftShoulder","clavicle_l"},{"LeftUpperArm","upperarm_l"},{"LeftLowerArm","lowerarm_l"},{"LeftHand","hand_l"},
            {"RightShoulder","clavicle_r"},{"RightUpperArm","upperarm_r"},{"RightLowerArm","lowerarm_r"},{"RightHand","hand_r"},
            {"LeftUpperLeg","thigh_l"},{"LeftLowerLeg","calf_l"},{"LeftFoot","foot_l"},{"LeftToes","ball_l"},
            {"RightUpperLeg","thigh_r"},{"RightLowerLeg","calf_r"},{"RightFoot","foot_r"},{"RightToes","ball_r"}};
        private bool Ours=>assetPath.StartsWith(Folder,StringComparison.Ordinal);
        private bool Clips=>assetPath==Folder+"ResidentClips.fbx";
        public override uint GetVersion()=>16; // bumped: stale Library imports in some lanes lacked the Interact take
        private void OnPreprocessModel()
        {
            if(!Ours)return;
            var model=(ModelImporter)assetImporter;
            model.importCameras=false;model.importLights=false;model.addCollider=false;model.isReadable=false;
            model.animationType=ModelImporterAnimationType.Human;model.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            // Explicit bone map: Unity's guess mirrored left/right on some male builds (they then faced and moved backwards).
            var d=model.humanDescription;d.human=Map.Select(kv=>new HumanBone{humanName=kv.Key,boneName=kv.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray();model.humanDescription=d;
            model.optimizeGameObjects=false; // head/neck bones are driven by FishingResidentBrain look-at
            model.importAnimation=Clips;model.animationCompression=ModelImporterAnimationCompression.Optimal;
            model.materialImportMode=Clips?ModelImporterMaterialImportMode.None:ModelImporterMaterialImportMode.ImportStandard;
            model.importBlendShapes=false;model.meshCompression=ModelImporterMeshCompression.Medium;
        }
        private void OnPreprocessAnimation()
        {
            if(!Clips)return;
            var model=(ModelImporter)assetImporter;
            model.clipAnimations=model.defaultClipAnimations.Select(c=>
            {
                var take=c.takeName.Substring(c.takeName.LastIndexOf('|')+1); // takes are "Armature|Idle_Loop"
                c.name=Used.TryGetValue(take,out var n)?n:take;c.loopTime=c.loopPose=Loops.Contains(c.name);
                // In-place use: bake root motion into the pose. Height follows the feet so kneel/crouch clips really go down.
                c.lockRootRotation=c.lockRootHeightY=c.lockRootPositionXZ=true;
                c.keepOriginalOrientation=true;c.rotationOffset=180; // UAL takes face the opposite way to the residents' avatars
                c.keepOriginalPositionY=false;c.heightFromFeet=true;c.keepOriginalPositionXZ=false;
                return c;
            }).ToArray();
        }
        private void OnPreprocessTexture()
        {
            if(!Ours)return;
            var texture=(TextureImporter)assetImporter;texture.maxTextureSize=1024;texture.mipmapEnabled=true;
            if(assetPath.EndsWith("Normal.png",StringComparison.Ordinal))texture.textureType=TextureImporterType.NormalMap;
        }
        private void OnPostprocessMaterial(Material material)
        {
            if(!Ours || !material.name.StartsWith("Resident",StringComparison.Ordinal))return;
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
            if(shader==null)throw new InvalidOperationException("Residents require the installed URP Lit shader.");
            material.shader=shader;
            var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+material.name+"Albedo.png");
            var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+material.name+"Normal.png");
            if(albedo==null || normal==null)throw new InvalidOperationException("Missing resident atlas for "+material.name);
            material.SetTexture("_BaseMap",albedo);material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.6f);material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.22f);
        }
    }
}
