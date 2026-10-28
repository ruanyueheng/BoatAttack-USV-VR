using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _albumPage;
        /// <summary>Journal page "相册图鉴": newest photos first on the left, the shared bay codex on the right.</summary>
        private void DrawAlbumPage(Rect r)
        {
            var c = Controller; var album = c.Life.Album; int pages = Mathf.Max(1, (album.Count + 8) / 9);
            _albumPage = Mathf.Clamp(_albumPage, 0, pages - 1);
            Label(r.x, r.y, 640, 30, "相册 · " + album.Count + " / " + FishingLifeWorld.AlbumLimit + " 张", 21, InkText, true);
            Label(r.x + 330, r.y + 4, 330, 24, "P 取景 · 左键拍照", 14, Muted);
            for (int i = 0; i < 9; i++)
            {
                int index = album.Count - 1 - (_albumPage * 9 + i);
                var cell = new Rect(r.x + (i % 3) * 218, r.y + 42 + (i / 3) * 144, 206, 136);
                Rounded(cell, InkRaised, 6);
                if (index < 0) { Label(new Rect(cell.x, cell.y + 50, cell.width, 30), "空白胶片", 13, Muted, false, TextAnchor.MiddleCenter); continue; }
                var photo = album[index]; var image = new Rect(cell.x + 6, cell.y + 6, 194, 109);
                if (photo.tex != null) GUI.DrawTexture(image, photo.tex, ScaleMode.ScaleAndCrop);
                Outline(image, Hairline, 2);
                string caption = "第 " + (photo.day + 1) + " 天" + (photo.entry >= 0 ? " · " + FishingPhotoCodex.Name(photo.entry) : "");
                Label(new Rect(cell.x + 6, cell.y + 115, 194, 20), caption, 12, photo.entry >= 0 ? Gold : Muted, false, TextAnchor.MiddleCenter);
            }
            if (Button(new Rect(r.x, r.y + 484, 150, 38), "‹ 较新", _albumPage > 0, false, 15)) _albumPage--;
            Label(new Rect(r.x + 160, r.y + 484, 320, 38), (_albumPage + 1) + " / " + pages, 15, Muted, false, TextAnchor.MiddleCenter);
            if (Button(new Rect(r.x + 490, r.y + 484, 150, 38), "较旧 ›", _albumPage < pages - 1, false, 15)) _albumPage++;

            var codex = new Rect(r.x + 680, r.y, r.width - 680, 527); Rounded(codex, InkRaised, 8);
            int photographed = 0; for (int e = 0; e < FishingPhotoCodex.Count; e++) if (FishingPhotoCodex.Photographed(c.Career, e)) photographed++;
            Label(codex.x + 20, codex.y + 14, 400, 30, "图鉴插图  " + photographed + " / " + FishingPhotoCodex.Count, 20, InkText, true);
            for (int e = 0; e < FishingPhotoCodex.Count; e++)
            {
                bool shot = FishingPhotoCodex.Photographed(c.Career, e), seen = FishingPhotoCodex.Seen(c.Career, e);
                var row = new Rect(codex.x + 16 + (e / 8) * 216, codex.y + 56 + (e % 8) * 58, 206, 52);
                if (shot) Outline(row, Gold, 6, 1);
                Label(new Rect(row.x + 8, row.y + 4, 196, 24), (shot ? "★ " : seen ? "· " : "？ ") + (seen ? FishingPhotoCodex.Name(e) : "未见条目"), 15, shot ? Gold : seen ? InkText : Muted, shot);
                Label(new Rect(row.x + 8, row.y + 27, 196, 22), shot ? "已拍下插图" : FishingPhotoCodex.Hint(e), 12, Muted);
            }
        }
    }
}
