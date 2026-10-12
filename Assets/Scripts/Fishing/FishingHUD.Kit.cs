using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Drawing primitives for the desktop HUD: one palette, rounded cards and custom buttons.</summary>
    public sealed partial class FishingHUD
    {
        private static readonly Color Ink = new Color(.025f, .048f, .082f, .96f);
        private static readonly Color InkRaised = new Color(.055f, .10f, .15f, .82f);
        private static readonly Color Hairline = new Color(.64f, .77f, .75f, .16f);
        private static readonly Color CardShadow = new Color(.08f, .18f, .19f, .16f);
        private static readonly Color Muted = new Color(.70f, .80f, .79f);
        private static readonly Color Faint = new Color(.56f, .68f, .68f);
        private static readonly Color Mint = new Color(.38f, .84f, .71f);
        private static readonly Color Gold = new Color(.96f, .72f, .35f);
        private static readonly Color Danger = new Color(1f, .46f, .37f);
        private static readonly Color Sky = new Color(.47f, .76f, .96f);
        private static readonly Color Track = new Color(.15f, .24f, .25f);
        private static readonly Color ButtonFill = new Color(.09f, .17f, .18f, .85f);
        private static readonly Color ButtonHover = new Color(.13f, .28f, .27f);
        private static readonly Color ButtonOff = new Color(.09f, .13f, .14f, .8f);
        private static readonly Color InkText = new Color(.92f, .96f, .93f);

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
            Rounded(new Rect(r.x-3, r.y+5, r.width+6, r.height), new Color(0,0,0,.3f), 10);
            Rounded(r, Ink, 10);
            Outline(r, accent*new Color(1,1,1,.35f), 10);
            Fill(r.x+24,r.y,Mathf.Min(84,r.width-48),2,accent);
            foreach(float x in new[]{r.x+9,r.xMax-9})Disc(new Vector2(x,r.y+9),1.5f,accent*new Color(1,1,1,.5f));
        }

        private void Label(Rect r, string text, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            _label.fontSize = size;
            // The bundled face is Medium; synthetic bold distorts small Chinese counters.
            _label.fontStyle = FontStyle.Normal;
            _label.normal.textColor = Faded(color == Color.white ? InkText : color);
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
            Color fill = !enabled ? ButtonOff : primary ? (hover ? new Color(.20f, .48f, .40f) : new Color(.12f, .34f, .29f)) : hover ? ButtonHover : ButtonFill;
            Rounded(r, fill, 6);
            Outline(r,!enabled?Hairline:primary?Mint*new Color(1,1,1,.55f):hover?Gold*new Color(1,1,1,.55f):Hairline,6);
            Color ink = !enabled ? Faint : primary ? Mint : Color.white;
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

        private static Color SpeciesColor(FishKind kind, bool rare)
        {
            Color body = kind == FishKind.Coral ? new Color(0.95f, 0.36f, 0.25f)
                : kind == FishKind.Dart ? new Color(0.25f, 0.6f, 0.88f)
                : kind == FishKind.Puffer ? new Color(0.9f, 0.7f, 0.25f) : new Color(0.2f, 0.42f, 0.78f);
            return rare ? Color.Lerp(body, Gold, 0.55f) : body;
        }

        /// <summary>A fish silhouette from rounded shapes, tinted per species, for cards and journal rows.</summary>
        private void FishGlyph(Rect r, FishKind kind, bool rare)
        {
            if((int)kind>=4)
            {
                if(!_fishThumbnails.TryGetValue(kind,out var thumbnail))
                {_fishThumbnails[kind]=thumbnail=Resources.Load<Texture2D>("FishingFish/"+kind+"Thumbnail");}
                if(thumbnail!=null)
                {
                    var color=GUI.color;GUI.color=Faded(rare?new Color(1,.88f,.65f):Color.white);
                    GUI.DrawTexture(r,thumbnail,ScaleMode.ScaleToFit,true);GUI.color=color;return;
                }
            }
            Color body = SpeciesColor(kind, rare);
            Color fin = new Color(body.r * 0.8f, body.g * 0.8f, body.b * 0.8f, 1f);
            float h = kind == FishKind.Puffer ? r.height * 0.74f : kind == FishKind.Sailfish ? r.height * 0.34f : r.height * 0.5f;
            var bodyRect = new Rect(r.x + r.width * 0.2f, r.center.y - h * 0.5f, r.width * 0.64f, h);
            var tailPivot = new Vector2(bodyRect.x + 4, r.center.y);
            float tail = Mathf.Max(8f, r.height * 0.18f);
            var matrix = GUI.matrix;
            Rotate(28f, tailPivot);
            Rounded(new Rect(tailPivot.x - r.width * 0.22f, tailPivot.y - tail * 0.5f, r.width * 0.24f, tail), fin, tail * 0.45f);
            GUI.matrix = matrix;
            Rotate(-28f, tailPivot);
            Rounded(new Rect(tailPivot.x - r.width * 0.22f, tailPivot.y - tail * 0.5f, r.width * 0.24f, tail), fin, tail * 0.45f);
            GUI.matrix = matrix;
            if (kind == FishKind.Sailfish)
            {
                // The golden sail rises along the back and a bill extends from the snout.
                Rounded(new Rect(bodyRect.x + bodyRect.width * 0.14f, bodyRect.y - r.height * 0.34f, bodyRect.width * 0.6f, r.height * 0.42f), Gold, r.height * 0.14f);
                Rounded(new Rect(bodyRect.xMax - 8, r.center.y - 2f, r.width * 0.22f, 4), fin, 2f);
            }
            Rounded(bodyRect, body, h * 0.5f);
            if (kind == FishKind.Coral)
                for (int i = 0; i < 2; i++) Rounded(new Rect(bodyRect.x + bodyRect.width * (0.36f + i * 0.2f), bodyRect.y + 2, Mathf.Max(3f, r.width * 0.05f), h - 4), new Color(1f, 0.95f, 0.85f, 0.9f), 2f);
            else if (kind == FishKind.Dart)
                Rounded(new Rect(bodyRect.x + 6, r.center.y - 1.5f, bodyRect.width - 16, 3), new Color(0.9f, 0.96f, 1f, 0.85f), 1.5f);
            else if (kind == FishKind.Puffer)
                for (int i = 0; i < 4; i++) Disc(new Vector2(bodyRect.x + bodyRect.width * (0.28f + i * 0.14f), r.center.y + (i % 2 == 0 ? -h * 0.18f : h * 0.14f)), Mathf.Max(1.5f, h * 0.06f), new Color(0.35f, 0.24f, 0.08f));
            Disc(new Vector2(bodyRect.xMax - h * 0.3f, r.center.y - h * 0.12f), Mathf.Max(2.5f, h * 0.1f), new Color(0.05f, 0.08f, 0.1f));
        }
        private readonly System.Collections.Generic.Dictionary<FishKind,Texture2D> _fishThumbnails=new System.Collections.Generic.Dictionary<FishKind,Texture2D>();
    }
}
