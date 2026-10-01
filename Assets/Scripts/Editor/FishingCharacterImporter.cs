using System;
using UnityEditor;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public sealed class FishingCharacterImporter : AssetPostprocessor
    {
        private bool Character => assetPath=="Assets/Resources/FishingCharacter/BeachCharacter.fbx";
        private void OnPreprocessModel()
        {
            if(!Character)return;
            var model=(ModelImporter)assetImporter;
            model.importCameras=false;model.importLights=false;model.addCollider=false;
            model.animationType=ModelImporterAnimationType.Legacy;model.importAnimation=true;
            model.animationCompression=ModelImporterAnimationCompression.Off;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
        public override uint GetVersion()=>2;
        private void OnPostprocessMaterial(Material material)
        {
            if(!Character)return;
            // 直接修改导入的子资源，不从赋材质回调返回未持久化的临时对象。
            var color=material.color;
            material.shader=Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.2f);
        }
    }
}
