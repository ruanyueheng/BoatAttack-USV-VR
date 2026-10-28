#!/usr/bin/env bash
# v0.44: Kenney RPG Audio (CC0) -> mono 44.1k ogg world cues; trim lead silence, ~-18 LUFS, short edge fades.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SRC="$ROOT/ArtSource/Audio/V044/2026-10-27/Originals"; OUT="$ROOT/Assets/Resources/FishingAudio/World"
while read -r key file; do
  ffmpeg -v error -y -i "$SRC/$file" -ac 1 -ar 44100 -af "silenceremove=start_periods=1:start_threshold=-50dB,loudnorm=I=-18:TP=-1.5:LRA=11,alimiter=limit=0.8:level=false,areverse,silenceremove=start_periods=1:start_threshold=-55dB,afade=t=in:d=0.03,areverse,afade=t=in:d=0.003,volume=-2.5dB" -c:a libvorbis -q:a 6 "$OUT/$key.ogg"
  sha=$(sha256sum "$SRC/$file" | cut -d' ' -f1); echo "$key <- $file sha256=$sha"
done <<'MAP'
BagOpen handleSmallLeather.ogg
BagClose handleSmallLeather2.ogg
BookOpen bookOpen.ogg
BookClose bookClose.ogg
CoinGain handleCoins.ogg
ChestOpen creak1.ogg
JumpPush cloth1.ogg
MAP
