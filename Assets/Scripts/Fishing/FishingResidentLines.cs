using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>v044 resident line table (Resources/FishingV044/Voice/lines.txt), shared with Tools/Art/build_v044_voice.py.</summary>
    public static class FishingResidentLines
    {
        private static Dictionary<string,string> _text;
        private static Dictionary<string,string> _byText;
        private static readonly Dictionary<string,AudioClip> _clips=new Dictionary<string,AudioClip>();
        private static void Load()
        {
            if(_text!=null)return;
            _text=new Dictionary<string,string>();_byText=new Dictionary<string,string>();
            var asset=Resources.Load<TextAsset>("FishingV044/Voice/lines");
            if(asset==null)throw new System.InvalidOperationException("Missing FishingV044/Voice/lines table");
            foreach(var raw in asset.text.Split('\n'))
            {
                var line=raw.TrimEnd('\r');if(line.Length==0 || line[0]=='#')continue;
                var cells=line.Split('\t');if(cells.Length!=3)throw new System.FormatException("Bad resident line: "+line);
                string key=cells[0]+"/"+cells[1],text=cells[2].Replace("\\n","\n");
                _text.Add(key,text);if(!cells[1].StartsWith("opt_"))_byText[text]=key;
            }
        }
        public static IEnumerable<KeyValuePair<string,string>> All{get{Load();return _text;}}
        public static bool Has(int id,string key){Load();return _text.ContainsKey(id+"/"+key);}
        public static string Text(int id,string key){Load();return _text.TryGetValue(id+"/"+key,out var t)?t:"";}
        /// <summary>Voice key of the first paragraph of a spoken text, or null when that paragraph has no recording.</summary>
        public static string VoiceKey(string speech)
        {
            Load();if(string.IsNullOrEmpty(speech))return null;
            int cut=speech.IndexOf("\n\n",System.StringComparison.Ordinal);var head=cut<0?speech:speech.Substring(0,cut);
            return _byText.TryGetValue(head,out var key)?key:null;
        }
        public static AudioClip Clip(string voiceKey)
        {
            if(voiceKey==null)return null;
            if(!_clips.TryGetValue(voiceKey,out var clip))_clips[voiceKey]=clip=Resources.Load<AudioClip>("FishingV044/Voice/"+voiceKey);
            return clip;
        }
        /// <summary>0 before the chart, 1 chart read, 2 some echoes back, 3 all four echoes.</summary>
        public static int Stage(FishingLore lore)=>lore.ClearCount>=4 || lore.Finale?3:lore.ClearCount>0?2:lore.ChartFound?1:0;
    }
}
