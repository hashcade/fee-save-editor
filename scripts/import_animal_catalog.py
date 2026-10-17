#!/usr/bin/env python3
"""Import the 24 adoptable animals and their official localized names."""

import argparse
import json
import xml.etree.ElementTree as ET
from pathlib import Path

from import_roster_catalog import fetch, load_texts, TEXT_REVISION

DATA_REVISION = "86b8be7b9820e1bb3bce87d2a9a805ead85d92ab"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = fetch("Xzonn/FireEmblemEngageData", DATA_REVISION, "data/xml/Animal.xml")
    texts = load_texts(["Person"])
    animals = []
    for row in ET.fromstring(source).findall("./Sheet/Data/Param"):
        animals.append({"Id": row.attrib["ANID"], "Names": {
            language: values[row.attrib["Name"]] for language, values in texts.items()}})
    if len(animals) != 24:
        raise ValueError("Expected 24 adoptable animal species.")
    args.output.write_text(json.dumps({"DataRevision": DATA_REVISION, "TextRevision": TEXT_REVISION,
                                      "Animals": animals}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Imported {len(animals)} animals in {len(texts)} languages.")


if __name__ == "__main__":
    main()
