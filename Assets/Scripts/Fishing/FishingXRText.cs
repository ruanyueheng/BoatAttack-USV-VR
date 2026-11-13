using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// Rewrites desktop key hints into PICO controller wording at every XR text sink.
    /// A/B/X/Y are PICO buttons and are never rewritten.
    /// </summary>
    public static class FishingXRText
    {
        // Whole phrases first: these read badly with a plain key swap.
        private static readonly (string from,string to)[] Phrases=
        {
            ("按住左键后摆，松开甩竿","拿竿后按住右扳机蓄力，松开抛竿"),
            ("按 Q 起航","按下右摇杆收竿起航"),("Q 起航","按下右摇杆收竿起航"),("按 Q 收起钓具","按下右摇杆收起钓具"),
            ("按住左键收线并移动鼠标 · 松开放线","左握把转腕收线 · 持竿手横移控鱼 · 松开放线"),
            ("按住左键，鼠标向右控竿","持竿手向右横移控鱼"),("按住左键，鼠标向左控竿","持竿手向左横移控鱼"),
            ("轻点近抛 · 按住远抛","轻点近抛 · 蓄力或挥腕增加抛距"),
            ("松开左键甩竿","松开扳机抛竿"),("按住左键收线","左握把转腕收线"),
            ("立刻松开左键","立刻松开握把"),("松开左键","松开握把"),
            ("Ctrl + W 加速","左握把 / 左摇杆按压加速"),("Shift疾跑","左握把疾跑"),("Shift加速","左握把加速"),
            ("空格跳跃","按下右摇杆跳跃"),("空格攀低台","按下右摇杆攀低台"),
            ("A/D 转向","右摇杆左右转向"),("A / D 转向","右摇杆左右转向"),("松开鼠标","松开扳机"),
            ("空格离开绳梯","到端自动离开"),("空格离开","到端自动离开"),("按住 E","按住右握把"),("（C）","（真实下蹲）"),
        };
        private static readonly (Regex key,string to)[] Keys=
        {
            (Key("WASD"),"左摇杆"),(new Regex("方向键"),"左摇杆"),(Key("Shift"),"左扳机"),(Key("Ctrl"),"真实下蹲"),
            (Key("Esc"),"B"),(Key("Tab"),"事务面板"),(Key("Enter"),"A"),
            (Key("E"),"A"),(Key("T"),"A"),(Key("G"),"X"),(Key("F"),"右扳机"),(Key("L"),"右扳机"),(Key("Q"),"事务面板"),(Key("R"),"手册"),
            (Key("I"),"手册"),(new Regex(@"(?<![A-Za-z0-9])J\s*查看"),"在手册中查看"),(Key("J"),"手册"),(new Regex("空格"),"A"),(Key("Space"),"A"),(Key("M"),"手册"),(Key("N"),"手册"),(Key("O"),"手册"),(Key("U"),"手册"),(Key("P"),"右扳机"),(Key("F1"),"求助石 A"),
            (new Regex("左键"),"右扳机"),(new Regex("右键"),"左握把"),(new Regex("鼠标"),"手柄"),(new Regex("数字键"),"Y"),
        };
        /// <summary>Segments that describe desktop-only controls (jump, camera toggles…) are dropped instead of translated.</summary>
        private static readonly Regex DesktopOnly=new Regex(@"(?<![A-Za-z0-9])(Alt|C|V|H|Z|S|W)(?![A-Za-z0-9])|滚轮");
        public static readonly Regex Forbidden=new Regex(@"(?<![A-Za-z0-9])(WASD|Shift|Ctrl|Space|Esc|Tab|Alt|Enter|F1|[EFGQRTIJMNOUPCVHZSW])(?![A-Za-z0-9])|左键|右键|鼠标|空格|方向键|数字键|滚轮");
        private static Regex Key(string k)=>new Regex(@"(?<![A-Za-z0-9])"+Regex.Escape(k)+@"(?![A-Za-z0-9])");
        private static readonly Dictionary<string,string> Cache=new Dictionary<string,string>();

        public static string Adapt(string text)=>XRSettings.isDeviceActive?Adapt(text,true):text;

        // World TextMesh labels keep their desktop source so entering/leaving VR rewrites them in place.
        private static readonly List<(UnityEngine.TextMesh label,string source)> Bound=new List<(UnityEngine.TextMesh,string)>();
        private static bool _boundXR;
        public static void Bind(UnityEngine.TextMesh label,string source)
        {
            int index=Bound.FindIndex(binding=>binding.label==label);
            if(index<0)Bound.Add((label,source));else Bound[index]=(label,source);
            label.text=Adapt(source,XRSettings.isDeviceActive);
            label.GetComponent<FishingWorldSign>()?.Refit();
        }
        /// <summary>Call once per frame (cheap bool compare); refreshes bound labels only when the XR state flips.</summary>
        public static void Tick(){bool xr=XRSettings.isDeviceActive;if(xr!=_boundXR)Refresh(xr);}
        public static void Refresh(bool xr)
        {
            _boundXR=xr;
            for(int i=Bound.Count-1;i>=0;i--)
            {
                var (label,source)=Bound[i];
                if(label==null){Bound.RemoveAt(i);continue;}
                label.text=Adapt(source,xr);label.GetComponent<FishingWorldSign>()?.Refit();
            }
        }
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBindings(){Bound.Clear();_boundXR=false;}
        public static string Adapt(string text,bool xr)
        {
            if(!xr || string.IsNullOrEmpty(text))return text;
            if(Cache.TryGetValue(text,out var done))return done;
            string s=text;
            foreach(var (from,to) in Phrases)s=s.Replace(from,to);
            var lines=s.Split('\n');
            for(int l=0;l<lines.Length;l++)
            {
                var parts=Regex.Split(lines[l],@"(\s*[·/／、，；]\s*)");
                var kept=new List<string>();
                for(int i=0;i<parts.Length;i+=2)
                {
                    string seg=parts[i];
                    if(DesktopOnly.IsMatch(Strip(seg)))continue;
                    foreach(var (key,to) in Keys)seg=key.Replace(seg,to);
                    if(kept.Count>0 && i>0)kept.Add(parts[i-1]);
                    kept.Add(seg);
                }
                lines[l]=string.Concat(kept).Trim();
            }
            s=string.Join("\n",lines);
            if(Cache.Count<4096)Cache[text]=s;
            return s;
        }
        // Units and names that look like keys but are not ("m", "km/h", "BGM", "VR").
        private static string Strip(string seg)=>Regex.Replace(seg,@"\d+(\.\d+)?\s*(m|km/h|s)\b|BGM|VR|XR|PICO|CC BY","");
    }
}
