using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        /// <summary>A bearing chart, not an invented coastline or an obstacle-avoiding route.</summary>
        private void DrawChart()
        {
            var c = Controller;
            if (!c.ChartExpanded)
            {
                if (Button(Layout.ChartTab, "M 航海图  ·  " + FishingPrototype.FlatDistance(c.ObserverPosition, c.NavigationPosition).ToString("0") + " m", true, false, 14)) c.ToggleChart();
                return;
            }
            var r = Layout.Chart;
            if (Button(new Rect(r.x, r.y, 156, 28), "M 收起航海图", true, false, 14)) c.ToggleChart();
            if (Button(new Rect(r.xMax - 60, r.y, 60, 28), c.ChartRangeMeters + "m", true, false, 13)) c.CycleChartRange();
            var center = new Vector2(r.center.x, r.y + 130);
            const float radius = 96;
            Disc(center, radius + 4, new Color(.02f, .065f, .08f, .7f));
            Disc(center, radius, new Color(.025f, .13f, .17f, .87f));
            Outline(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), new Color(.52f, .82f, .85f, .55f), radius, 1.5f);
            Outline(new Rect(center.x - radius * .5f, center.y - radius * .5f, radius, radius), new Color(.4f, .7f, .76f, .17f), radius * .5f);
            Segment(center + Vector2.left * 88, center + Vector2.right * 88, new Color(.4f, .7f, .76f, .12f));
            Segment(center + Vector2.up * 88, center + Vector2.down * 88, new Color(.4f, .7f, .76f, .12f));
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                Segment(center + dir * (i % 6 == 0 ? 86 : 91), center + dir * 94, i % 6 == 0 ? Muted : Faint * new Color(1, 1, 1, .45f));
            }
            Label(new Rect(center.x - 10, center.y - 87, 20, 20), "N", 13, Mint, true, TextAnchor.MiddleCenter);
            Vector2 target = ChartPoint(c.NavigationPosition, center);
            Vector2 bearing = target - center;
            float distance = bearing.magnitude;
            if (distance > 22)
                for (float d = 16; d < distance - 13; d += 10)
                    Segment(center + bearing.normalized * d, center + bearing.normalized * Mathf.Min(d + 4, distance - 13), new Color(1, .77f, .42f, .65f), 1.5f);
            ChartMarker(c.harbor, center, "港", 0, Mint);
            if (c.IsAshore)
            {
                var boarding = ChartPoint(c.Shore.BoardingPoint, center);
                Disc(boarding, 10, Mint);
                Label(new Rect(boarding.x - 12, boarding.y - 12, 24, 24), "船", 13, InkText, true, TextAnchor.MiddleCenter);
            }
            for (int i = 0; i < c.fishingSpots.Length; i++) ChartMarker(c.fishingSpots[i], center, (i + 1).ToString(), i + 1, Gold);
            // The player is always centered; its arrow rotates while north stays fixed.
            Disc(center, 12, Ink);
            DrawArrow(center, c.IsAshore ? c.viewCamera.transform.eulerAngles.y : c.Player.transform.eulerAngles.y, Color.white, 10);
            Label(new Rect(r.x, r.yMax - 18, r.width, 20), "北向固定 · 方位参考，注意礁石", 12, Muted, false, TextAnchor.MiddleCenter);
        }

        private Vector2 ChartPoint(Vector3 world, Vector2 center)
        {
            var p = Controller.ObserverPosition;
            var point = FishingChartMath.Project(world.x, world.z, p.x, p.z, Controller.ChartRangeMeters, 72);
            return center + new Vector2(point.X, point.Y);
        }

        private void ChartMarker(Vector3 world, Vector2 center, string text, int target, Color color)
        {
            var c = Controller;
            var p = ChartPoint(world, center);
            bool selected = c.NavigationTarget == target;
            bool offRange = FishingPrototype.FlatDistance(c.ObserverPosition, world) > c.ChartRangeMeters;
            if (selected) Outline(new Rect(p.x - 17, p.y - 17, 34, 34), Gold, 17, 2);
            Disc(p, 12, offRange ? Ink : color);
            if (offRange) Outline(new Rect(p.x - 12, p.y - 12, 24, 24), color, 12, 1.5f);
            Label(new Rect(p.x - 12, p.y - 12, 24, 24), text, 14, offRange ? color : InkText, true, TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(p.x - 16, p.y - 16, 32, 32), GUIContent.none, GUIStyle.none)) c.SelectNavigation(target);
        }

        private void Segment(Vector2 from, Vector2 to, Color color, float width = 1f)
        {
            var matrix = GUI.matrix;
            Rotate(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, from);
            Fill(from.x, from.y - width * .5f, Vector2.Distance(from, to), width, color);
            GUI.matrix = matrix;
        }

        private void DrawArrow(Vector2 center, float degrees, Color color, float size)
        {
            float a = degrees * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            var right = new Vector2(-forward.y, forward.x);
            var tip = center + forward * size;
            var rear = center - forward * size * .6f;
            Segment(rear + right * size * .55f, tip, color, 2.5f);
            Segment(tip, rear - right * size * .55f, color, 2.5f);
            Segment(rear, center, color, 2.5f);
        }
    }
}
