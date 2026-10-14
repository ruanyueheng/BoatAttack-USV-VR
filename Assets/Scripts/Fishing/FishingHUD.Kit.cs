using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Drawing primitives for the desktop HUD: one palette, rounded cards and custom buttons.</summary>
    public sealed partial class FishingHUD
    {
        private static readonly Color Ink = FishingUITheme.Paper;
        private static readonly Color InkRaised = FishingUITheme.Light;
        private static readonly Color Hairline = new Color(.65f,.48f,.29f,.38f);
        private static readonly Color CardShadow = new Color(.08f, .18f, .19f, .16f);
        private static readonly Color Muted = FishingUITheme.Muted;
        private static readonly Color Faint = new Color(.36f,.41f,.35f);
        private static readonly Color Mint = FishingUITheme.Sage;
        private static readonly Color Gold = FishingUITheme.Gold;
        private static readonly Color Danger = new Color(.71f,.28f,.18f);
        private static readonly Color Sky = new Color(.21f,.42f,.42f);
        private static readonly Color Track = new Color(.76f,.77f,.65f);
        private static readonly Color ButtonFill = FishingUITheme.Light;
        private static readonly Color ButtonHover = new Color(.88f,.93f,.82f);
        private static readonly Color ButtonOff = FishingUITheme.Disabled;
        private static readonly Color InkText = FishingUITheme.Ink;
        private Rect _pressedButton;

        // Opacity multiplier for everything drawn through this kit (used by fading cards).
        private float _fade = 1f;

        private Color Faded(Color color) => new Color(color.r, color.g, color.b, color.a * _fade);

        private void Rounded(Rect r, Color color, float radius)
            => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Faded(color), 0, radius);

        private void Outline(Rect r, Color color, float radius, float width = 1f)
            => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Faded(color), width, radius);

        private void Fill(float x, float y, float width, float height, Color color)
            => GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Faded(color), 0, 0);

        private void Disc(Vector2 centre, float radius, Color color)
            => Rounded(new Rect(centre.x - radius, centre.y - radius, radius * 2, radius * 2), color, radius);

        /// <summary>
        /// RotateAroundPivot expects a screen-space pivot, but this HUD draws through a scaled GUI.matrix,
        /// so the logical pivot is transformed first. Callers restore GUI.matrix afterwards.
        /// </summary>
        private static void Rotate(float degrees, Vector2 logicalPivot)
        {
            Vector3 screen = GUI.matrix.MultiplyPoint3x4(new Vector3(logicalPivot.x, logicalPivot.y, 0f));
            GUIUtility.RotateAroundPivot(degrees, new Vector2(screen.x, screen.y));
        }

        /// <summary>Raised card: soft drop shadow, warm ivory body, hairline edge and an accent tick.</summary>
        private void Card(Rect r, Color accent)
        {
            Rounded(new Rect(r.x, r.y+3, r.width, r.height), new Color(.20f,.17f,.10f,.14f), 10);
            Rounded(r, Ink, 10);
            Outline(r, Gold*new Color(1,1,1,.65f), 10);
            Outline(new Rect(r.x+4,r.y+4,r.width-8,r.height-8), Hairline*.45f, 8);
            Fill(r.x+24,r.y,Mathf.Min(84,r.width-48),2,accent);
            foreach(float x in new[]{r.x+9,r.xMax-9})Disc(new Vector2(x,r.y+9),1.5f,accent*new Color(1,1,1,.5f));
        }

        private void Label(Rect r, string text, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            _label.fontSize = size;
            _label.font=bold && size>=25?FishingUITheme.Heading:_font;
            // The bundled face is Medium; synthetic bold distorts small Chinese counters.
            _label.fontStyle = FontStyle.Normal;
            _label.normal.textColor = Faded(color==Color.white?InkText:color==Gold?new Color(.48f,.32f,.15f):color==Mint?new Color(.20f,.39f,.30f):color);
            _label.alignment = align;
            GUI.Label(r, text, _label);
        }

        private void Label(float x, float y, float width, float height, string text, int size, Color color, bool bold = false)
            => Label(new Rect(x, y, width, height), text, size, color, bold);

        /// <summary>
        /// Custom button. A disabled button still reserves its IMGUI control so the IDs of later
        /// controls stay stable between layout and repaint passes.
        /// </summary>
        private bool Button(Rect r, string text, bool enabled = true, bool primary = false, int size = 17)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            if(Event.current.rawType==EventType.MouseDown && Event.current.button==0 && hover)_pressedButton=r;
            bool pressed=enabled && _pressedButton==r && GUIUtility.hotControl!=0;
            Color fill = !enabled ? ButtonOff : pressed?FishingUITheme.Selected:primary?FishingUITheme.Selected:hover?ButtonHover:ButtonFill;
            Rounded(r, fill, 6);
            Outline(r,!enabled?Hairline:pressed?InkText:primary||hover?Mint:Hairline,6,pressed||primary?2:1);
            Color ink = !enabled ? Faint : Color.white;
            Label(r, text, size, ink, true, TextAnchor.MiddleCenter);
            return GUI.Button(r, GUIContent.none, GUIStyle.none) && enabled;
        }

        private bool Tab(Rect r, string text, bool selected)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (selected) Fill(r.x+12,r.yMax-3,r.width-24,2,Mint);
            else
            {
                if(hover)Rounded(r,ButtonHover,6);
            }
            Label(r, text, 17, selected ? Mint : Muted, true, TextAnchor.MiddleCenter);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Pill-shaped tag; returns its width so callers can lay out a row.</summary>
        private float Chip(float x, float y, string text, Color color, bool filled = false, int size = 13)
        {
            _label.font=_font;
            _label.fontSize = size;
            _label.fontStyle = FontStyle.Normal;
            float width = _label.CalcSize(new GUIContent(text)).x + 18;
            var r = new Rect(x, y, width, size + 11);
            if (filled) Rounded(r, color, r.height * 0.5f);
            else
            {
                Rounded(r, color * new Color(1, 1, 1, 0.16f), r.height * 0.5f);
                Outline(r, color * new Color(1, 1, 1, 0.55f), r.height * 0.5f);
            }
            Label(r, text, size, filled ? Ink : color, true, TextAnchor.MiddleCenter);
            return width;
        }

        private void Bar(Rect r, float value, Color fill)
        {
            Rounded(r, Track, r.height * 0.5f);
            float w = Mathf.Clamp01(value) * r.width;
            if (w > 0.5f) Rounded(new Rect(r.x, r.y, Mathf.Max(w, r.height), r.height), fill, r.height * 0.5f);
        }

        private void Pips(float x, float y, int filled, int total, Color color)
        {
            for (int i = 0; i < total; i++) Rounded(new Rect(x + i * 15, y, 10, 10), i < filled ? color : Track, 5);
        }

        private void Section(float x, float y, float width, string title, string note)
        {
            Label(x, y, width, 24, title, 16, Color.white, true);
            if (note.Length > 0) Label(new Rect(x, y + 2, width, 22), note, 13, Faint, false, TextAnchor.UpperRight);
            Fill(x, y + 26, width, 1, Hairline);
        }

        private void WeatherIcon(Rect r, BayWeather weather)
        {
            var c = r.center;
            var cloud = new Color(0.86f, 0.92f, 0.96f);
            switch (weather)
            {
                case BayWeather.Clear:
                    Disc(c, r.width * 0.42f, Gold * new Color(1, 1, 1, 0.25f));
                    Disc(c, r.width * 0.28f, Gold);
                    break;
                case BayWeather.Cloudy:
                    Disc(c + new Vector2(-8, 2), r.width * 0.22f, new Color(0.7f, 0.8f, 0.88f));
                    Disc(c + new Vector2(6, -3), r.width * 0.28f, cloud);
                    Rounded(new Rect(r.x + 6, c.y + 2, r.width - 12, r.height * 0.26f), cloud, r.height * 0.13f);
                    break;
                case BayWeather.Rain:
                    Disc(c + new Vector2(4, -8), r.width * 0.26f, cloud);
                    Rounded(new Rect(r.x + 6, c.y - 6, r.width - 12, r.height * 0.24f), cloud, r.height * 0.12f);
                    for (int i = 0; i < 3; i++) Rounded(new Rect(r.x + 13 + i * 10, c.y + 10, 3, 11), Sky, 1.5f);
                    break;
                case BayWeather.Mist:
                    for (int i = 0; i < 3; i++) Rounded(new Rect(r.x + 5 + (i % 2) * 7, r.y + 13 + i * 10, r.width - 17, 5), cloud * new Color(1, 1, 1, 0.9f - i * 0.2f), 2.5f);
                    break;
                default:
                    Disc(c + new Vector2(0, 6), r.width * 0.3f, new Color(1f, 0.55f, 0.32f));
                    Fill(r.x + 2, c.y + 7, r.width - 4, r.height * 0.5f - 7, Ink);
                    Rounded(new Rect(r.x + 4, c.y + 7, r.width - 8, 3), Gold, 1.5f);
                    break;
            }
        }

        /// <summary>All catalog entries use portraits of their actual game meshes.</summary>
        private void FishGlyph(Rect r, FishKind kind, bool rare)
        {
            if(!_fishThumbnails.TryGetValue(kind,out var thumbnail))
                _fishThumbnails[kind]=thumbnail=Resources.Load<Texture2D>("FishingFish/"+kind+"Thumbnail");
            if(thumbnail==null)return;
            var color=GUI.color;GUI.color=Faded(rare?new Color(1,.88f,.65f):Color.white);
            GUI.DrawTexture(r,thumbnail,ScaleMode.ScaleToFit,true);GUI.color=color;
        }
        private readonly System.Collections.Generic.Dictionary<FishKind,Texture2D> _fishThumbnails=new System.Collections.Generic.Dictionary<FishKind,Texture2D>();
    }
}
