using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingInteractionAnimationBaker
    {
        private struct Pose
        {
            public Vector3 Position,Scale;public Quaternion Rotation;
            public Pose(Transform t){Position=t.localPosition;Scale=t.localScale;Rotation=t.localRotation;}
            public void Apply(Transform t){t.localPosition=Position;t.localRotation=Rotation;t.localScale=Scale;}
        }
        private static Dictionary<string,Transform> Bones(GameObject root)=>root.GetComponentsInChildren<Transform>(true)
            .ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,root.transform),t=>t);
        public static void Bake()
        {
            const string sourcePath="Assets/Resources/FishingCharacter/InteractionAnimations.fbx";
            const string targetPath="Assets/Resources/FishingCharacter/9SCharacter.fbx";
            const string output="Assets/Resources/FishingCharacter/CompatibleInteractions";
            Directory.CreateDirectory(output);
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
            var target=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(targetPath));
            source.hideFlags=target.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                foreach(var animation in source.GetComponentsInChildren<Animation>())animation.enabled=false;
                foreach(var animation in target.GetComponentsInChildren<Animation>())animation.enabled=false;
                var sb=Bones(source);var tb=Bones(target);
                var sr=sb.ToDictionary(x=>x.Key,x=>new Pose(x.Value));var tr=tb.ToDictionary(x=>x.Key,x=>new Pose(x.Value));
                var paths=sb.Keys.Where(p=>p.Length>0 && tb.ContainsKey(p)).ToArray();
                foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().Where(x=>!x.name.StartsWith("__")))
                {
                    var keys=paths.ToDictionary(p=>p,p=>Enumerable.Range(0,10).Select(_=>new List<Keyframe>()).ToArray());
                    int samples=Mathf.CeilToInt(clip.length*30);
                    for(int frame=0;frame<=samples;frame++)
                    {
                        foreach(var pair in sb)sr[pair.Key].Apply(pair.Value);
                        float time=Mathf.Min(clip.length,frame/30f);clip.SampleAnimation(source,time);
                        foreach(string path in paths)
                        {
                            var current=sb[path];var from=sr[path];var to=tr[path];
                            float ratio=from.Position.magnitude>.0001f?to.Position.magnitude/from.Position.magnitude:1;
                            var position=to.Position+(current.localPosition-from.Position)*ratio;
                            var rotation=to.Rotation*Quaternion.Inverse(from.Rotation)*current.localRotation;
                            var scale=new Vector3(to.Scale.x*SafeRatio(current.localScale.x,from.Scale.x),to.Scale.y*SafeRatio(current.localScale.y,from.Scale.y),to.Scale.z*SafeRatio(current.localScale.z,from.Scale.z));
                            float[] values={position.x,position.y,position.z,rotation.x,rotation.y,rotation.z,rotation.w,scale.x,scale.y,scale.z};
                            for(int i=0;i<values.Length;i++)keys[path][i].Add(new Keyframe(time,values[i]));
                        }
                    }
                    string name=clip.name.Split('|').Last();var baked=new AnimationClip{name=name,legacy=true,frameRate=30,wrapMode=WrapMode.ClampForever};
                    string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
                    foreach(string path in paths)for(int i=0;i<properties.Length;i++)
                    {
                        string bone=path.Split('/').Last();
                        if(name=="Punch_Right" && (bone=="Body" || bone.StartsWith("UpperLeg") || bone.StartsWith("LowerLeg") || bone.StartsWith("Ankle") || bone.StartsWith("Foot")))continue;
                        var curveKeys=keys[path][i];
                        if(curveKeys.All(k=>Mathf.Abs(k.value-curveKeys[0].value)<.000001f))curveKeys=new List<Keyframe>{curveKeys[0],curveKeys[curveKeys.Count-1]};
                        AnimationUtility.SetEditorCurve(baked,EditorCurveBinding.FloatCurve(path,typeof(Transform),properties[i]),new AnimationCurve(curveKeys.ToArray()));
                    }
                    baked.EnsureQuaternionContinuity();string assetPath=output+"/"+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    if(existing==null)AssetDatabase.CreateAsset(baked,assetPath);
                    else{EditorUtility.CopySerialized(baked,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(baked);}
                    Debug.Log("[FEEDBACK-P3] Baked Unity-native same-rig action "+name+"; bones="+paths.Length+" frames="+(samples+1));
                }
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            }
            finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(target);}
        }
        private static float SafeRatio(float value,float basis)=>Mathf.Abs(basis)>.00001f?value/basis:1;
    }
}
