using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private static string AudioPercent(float value)=>(value*100).ToString("0")+"%";
        private void SetAudioLevel(string key,float value)
        {
            _c.SetSetting(key,Mathf.Clamp01(value));_c.SaveSettings();Refresh();
        }
        private void RefreshAudioPage()
        {
            _status.text="声音混音\n总音量 "+AudioPercent(_c.MasterVolume)
                +"\nBGM 背景音乐 "+AudioPercent(_c.MusicVolume)
                +"\n环境：引擎 / 海浪 / 风雨 "+AudioPercent(_c.AmbientVolume)
                +"\n音效：脚步 / 拾取 / 鱼轮 / 枪声 "+AudioPercent(_c.EffectsVolume)
                +"\n角色语音 "+AudioPercent(_c.VoiceVolume)
                +"\n\n背景声太响时，先降低环境音。\n总音量每次25%，BGM/环境每次10%，音效/语音每次25%。\n与电脑版共用音量设置并自动保存。";
            Set(0,"总音量 "+AudioPercent(_c.MasterVolume)+"\n点击依次切换 0 / 25 / 50 / 75 / 100%",
                ()=>SetAudioLevel("Master",(Mathf.FloorToInt(_c.MasterVolume*4+.001f)+1)%5*.25f));
            Set(1,_c.QuietMode?"解除静音":"全部静音",()=>{_c.ToggleQuiet();_c.SaveSettings();Refresh();});
            Set(2,"BGM 降低\n背景音乐 "+AudioPercent(_c.MusicVolume),()=>SetAudioLevel("Music",_c.MusicVolume-.1f));
            Set(3,"BGM 提高\n背景音乐 "+AudioPercent(_c.MusicVolume),()=>SetAudioLevel("Music",_c.MusicVolume+.1f));
            Set(4,"环境降低\n引擎 / 海浪 / 风雨 "+AudioPercent(_c.AmbientVolume),()=>SetAudioLevel("Ambient",_c.AmbientVolume-.1f));
            Set(5,"环境提高\n引擎 / 海浪 / 风雨 "+AudioPercent(_c.AmbientVolume),()=>SetAudioLevel("Ambient",_c.AmbientVolume+.1f));
            // Nine slots only: effects and voice each cycle 0/25/50/75/100% like the master button.
            Set(6,"音效 "+AudioPercent(_c.EffectsVolume)+"\n点击切换",()=>SetAudioLevel("Effects",(Mathf.FloorToInt(_c.EffectsVolume*4+.001f)+1)%5*.25f));
            Set(7,"角色语音 "+AudioPercent(_c.VoiceVolume)+"\n点击切换",()=>SetAudioLevel("Voice",(Mathf.FloorToInt(_c.VoiceVolume*4+.001f)+1)%5*.25f));
            Set(8,"返回设置",()=>Open(14));
        }
    }
}
