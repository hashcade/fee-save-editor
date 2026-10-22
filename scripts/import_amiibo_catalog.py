#!/usr/bin/env python3
"""Import official names for the explicit 24-reward amiibo whitelist."""

import argparse
import json
from pathlib import Path

from import_roster_catalog import load_texts, TEXT_REVISION

OUTFITS = [
    "英雄王の衣装", "慈愛の王女の衣装", "聖騎士の衣装", "賢王の衣装",
    "若き獅子の衣装", "草原の公女の衣装", "碧き風空の衣装", "蒼炎の勇者の衣装",
    "暁の巫女の衣装", "聖王女の衣装", "未来を選びし者の衣装", "導き手の衣装",
]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    texts = load_texts(["Accessories", "MusicName"])
    rewards = []
    for outfit in OUTFITS:
        matches = [key for key, value in texts["ja"].items() if value == outfit and key.startswith("MAID_")]
        if len(matches) != 1:
            raise ValueError(f"Expected one official outfit name: {outfit}")
        rewards.append({"Key": "G_所持_AID_" + outfit, "Group": "Outfits",
                        "Names": {language: values[matches[0]] for language, values in texts.items()}})
    for number in range(3, 15):
        key = f"BGM_Amiibo_{number:02}_ST_Play"
        rewards.append({"Key": "G_" + key, "Group": "Music",
                        "Names": {language: values["MID_" + key] for language, values in texts.items()}})
    args.output.write_text(json.dumps({"TextRevision": TEXT_REVISION, "Rewards": rewards},
                                     ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Imported {len(rewards)} amiibo rewards in {len(texts)} languages.")


if __name__ == "__main__":
    main()
