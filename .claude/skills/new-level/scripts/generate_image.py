#!/usr/bin/env python3
"""Generates source art with Gemini's image models (Nano Banana), for scripts/import_level.py.

    export GEMINI_API_KEY=...
    python3 .claude/skills/new-level/scripts/generate_image.py \
        "a red toadstool with white spots, flat colours" --out /tmp/toadstool.png

Only the standard library, so there is nothing to install. The image is written as-is; turning
it into a level is import_level.py's job, and it is the one that decides whether the picture
makes a playable puzzle.

Model IDs, newest first (https://ai.google.dev/gemini-api/docs/image-generation):
    gemini-3.1-flash-image       Nano Banana 2   (default)
    gemini-3.1-flash-lite-image  Nano Banana 2 Lite
    gemini-3-pro-image           Nano Banana Pro
    gemini-2.5-flash-image       Nano Banana, legacy

Free quota depends on the account and the model, and Google moves it around. If the API says no,
it says so in its own words; this script passes the error straight through rather than guessing.
"""

import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/interactions"
DEFAULT_MODEL = os.environ.get("GEMINI_IMAGE_MODEL", "gemini-3.1-flash-image")


def generate(prompt, model, api_key):
    """The generated image's bytes."""
    body = json.dumps({"model": model, "input": [{"type": "text", "text": prompt}]}).encode()
    request = urllib.request.Request(
        ENDPOINT,
        data=body,
        headers={"x-goog-api-key": api_key, "Content-Type": "application/json"},
    )

    try:
        with urllib.request.urlopen(request, timeout=180) as response:
            payload = json.load(response)
    except urllib.error.HTTPError as error:
        detail = error.read().decode(errors="replace").strip()
        raise SystemExit(f"Gemini returned {error.code}: {detail}") from error
    except urllib.error.URLError as error:
        raise SystemExit(f"could not reach the Gemini API: {error.reason}") from error

    data = _image_data(payload)
    if data is None:
        raise SystemExit(f"no image in the response: {json.dumps(payload)[:400]}")

    return base64.b64decode(data)


def _image_data(payload):
    """The base64 image, from wherever this response shape keeps it."""
    image = payload.get("output_image")
    if isinstance(image, dict) and image.get("data"):
        return image["data"]

    for step in payload.get("steps", []):
        for part in step.get("content", []):
            if part.get("type") == "image" and part.get("data"):
                return part["data"]

    return None


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("prompt", help="what to draw. See the skill for what makes a good level")
    parser.add_argument("--out", type=Path, required=True, help="where to write the PNG")
    parser.add_argument("--model", default=DEFAULT_MODEL, help="default: %(default)s")
    args = parser.parse_args(argv)

    api_key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
    if not api_key:
        raise SystemExit(
            "set GEMINI_API_KEY first. Get one from https://aistudio.google.com/apikey, then\n"
            "  export GEMINI_API_KEY=..."
        )

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_bytes(generate(args.prompt, args.model, api_key))
    print(f"wrote {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
