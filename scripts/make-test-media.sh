#!/usr/bin/env bash
# Generates a small synthetic TV library (two shows, two seasons each) so episode-ordering
# behaviour can be exercised. Output goes to media/ (git-ignored). Requires ffmpeg.
set -euo pipefail
cd "$(dirname "$0")/.."
DUR=${DUR:-30}

gen() { # show season episode
  local show=$1 season=$2 ep=$3
  local dir="media/${show}/Season $(printf %02d "$season")"
  local out="${dir}/${show} - S$(printf %02d "$season")E$(printf %02d "$ep").mp4"
  [ -f "$out" ] && return 0
  mkdir -p "$dir"
  echo "  $out"
  ffmpeg -loglevel error -y \
    -f lavfi -i "testsrc2=size=640x360:rate=24" \
    -f lavfi -i "sine=frequency=$((300 + season * 100 + ep * 20)):sample_rate=48000" \
    -t "$DUR" \
    -vf "drawtext=text='${show} S${season}E${ep}':fontsize=36:fontcolor=white:x=(w-text_w)/2:y=(h-text_h)/2" \
    -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p \
    -c:a aac -b:a 64k -ac 2 \
    -movflags +faststart \
    "$out"
}

for show in "Alpha Show" "Beta Show"; do
  for season in 1 2; do
    for ep in 1 2 3; do gen "$show" "$season" "$ep"; done
  done
done

touch media/.generated
echo "Done."
