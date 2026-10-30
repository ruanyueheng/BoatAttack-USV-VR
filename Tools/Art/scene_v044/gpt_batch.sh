#!/bin/sh
# usage: gpt_batch.sh <txt>...  — renders each prompt file to the png beside it via the shared ChatGPT /images tab
cd "$(dirname "$0")"
for t in "$@"; do p="${t%.txt}.png"; [ -f "$p" ] && continue; node gpt_image.js "$t" "$p" 2>&1 | cut -c1-80; done
