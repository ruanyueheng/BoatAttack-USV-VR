using UnityEngine;
namespace BoatAttack.Fishing
{
    public static class FishingUIFont
    {
        private static Font _font;
        public static Font Font => _font!=null?_font:(_font=Resources.Load<Font>("Fonts/TideSansSC") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
    }
}
