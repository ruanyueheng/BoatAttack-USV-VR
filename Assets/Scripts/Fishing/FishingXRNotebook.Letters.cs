namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        public const int LettersPage = 24;
        /// <summary>Re-read drift letters, replies and market cards; the letter opens its own paper, so the notebook closes first.</summary>
        private void RefreshLettersPage()
        {
            var letters = FishingSeaMarket.Archive(_c.Career.Lore);
            _status.fontSize = 21;
            _status.text = letters.Count == 0 ? "信件\n还没有收到信。\n出海捞起漂流瓶，或在集市船上放一封信。"
                : "信件 · " + (_offset / 6 + 1) + " / " + ((letters.Count + 5) / 6) + "\n扳机点选一封重读";
            for (int i = 0; i < 6; i++)
            {
                int index = _offset + i; if (index >= letters.Count) break;
                var l = letters[index];
                Set(i, l.title + "\n—— " + l.sender, () => { Close(); _c.Letter.Show(l.title, l.sender, l.body); });
            }
            Pager(letters.Count);
        }
    }
}
