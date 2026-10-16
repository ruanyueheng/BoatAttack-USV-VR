using System;
using UnityEditor;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public sealed class FishingCharacterImporter : AssetPostprocessor
    {
        private bool Character => assetPath=="Assets/Resources/FishingCharacter/BeachCharacter.fbx" || assetPath=="Assets/Resources/FishingCharacter/9SCharacter.fbx";
        private void OnPreprocessModel()
        {
            if(!Character)return;
            var model=(ModelImporter)assetImporter;
            model.importCameras=false;model.importLights=false;model.addCollider=false;
            model.isReadable=true; // 运行时一次性生成本地头颈三角形遮罩。
            model.animationType=ModelImporterAnimationType.Legacy;model.importAnimation=true;
            model.animationCompression=ModelImporterAnimationCompression.Off;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
        public override uint GetVersion()=>5;
        private void OnPostprocessMaterial(Material material)
        {
            if(!Character)return;
            // 直接修改导入的子资源，不从赋材质回调返回未持久化的临时对象。
            var color=material.color;
            // 冷导入时 Shader.Find 的全局注册表可能尚未包含 URP；显式引用包资源，不能写入空 Shader。
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
            if(shader==null)throw new InvalidOperationException("Beach Character requires the installed URP Lit shader.");
            material.shader=shader;
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.2f);
            if(material.name.StartsWith("9S",StringComparison.Ordinal))
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/FishingCharacter/"+material.name+"Albedo.png");
                if(texture==null)throw new InvalidOperationException("Missing supplied character texture: "+material.name);
                material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
                material.SetFloat("_Cull",0);
            }
        }
    }
}
