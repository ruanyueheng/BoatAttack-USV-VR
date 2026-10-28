using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>V044: no keyboard key names may reach a PICO player. Run from FishingV028UIValidation.</summary>
    public static class FishingXRTextValidation
    {
        private static readonly Regex Literal=new Regex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"");
        private static readonly Regex Chinese=new Regex("[一-鿿]");

        /// <summary>Every Chinese string literal in the game scripts, adapted for XR, must be free of desktop key names.</summary>
        public static List<string> LiteralFailures()
        {
            var failures=new List<string>();
            foreach(var path in Directory.GetFiles("Assets/Scripts/Fishing","*.cs"))
            {
                var lines=File.ReadAllLines(path);
                for(int n=0;n<lines.Length;n++)foreach(Match m in Literal.Matches(lines[n]))
                {
                    string raw=m.Groups[1].Value;
                    if(!Chinese.IsMatch(raw))continue;
                    string text=raw.Replace("\\n","\n").Replace("\\\"","\"");
                    string xr=FishingXRText.Adapt(text,true);
                    if(FishingXRText.Forbidden.IsMatch(xr))failures.Add(Path.GetFileName(path)+":"+(n+1)+" "+xr.Replace('\n',' '));
                }
            }
            return failures;
        }

        /// <summary>A sign built on desktop is rewritten when XR turns on, and restored when it turns off.</summary>
        public static string SignRefreshFailure()
        {
            var root=new GameObject("XR text validation");
            try
            {
                FishingXRText.Refresh(false);
                var sign=FishingV019Art.Sign("E 躺下休息至清晨 · Esc 返回",root.transform,Vector3.zero,.01f,Color.white);
                if(sign.text!="E 躺下休息至清晨 · Esc 返回")return "Desktop sign was altered: "+sign.text;
                FishingXRText.Refresh(true);
                if(FishingXRText.Forbidden.IsMatch(sign.text) || !sign.text.Contains("A"))return "Sign not rewritten after entering XR: "+sign.text;
                FishingXRText.Refresh(false);
                if(sign.text!="E 躺下休息至清晨 · Esc 返回")return "Sign not restored after leaving XR: "+sign.text;
                return null;
            }
            finally{Object.DestroyImmediate(root);FishingXRText.Refresh(false);}
        }
    }
}
