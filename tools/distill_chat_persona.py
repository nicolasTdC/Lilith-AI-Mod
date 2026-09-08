#!/usr/bin/env python3
"""Extract chat turns for one speaker and write a local persona overlay.

The overlay is meant for a private folder (not git). This script never embeds
the speaker's real name into tracked source; pass it only as a CLI argument.

Expected transcript shape (WhatsApp/Google Chat style):

    SpeakerName
     —
    DD/MM/YYYY, HH:MM
    message text

Usage:
    python tools/distill_chat_persona.py \\
        --input /path/to/chat.txt \\
        --speaker "SpeakerName" \\
        --out /path/to/private/persona
"""
from __future__ import annotations

import argparse
import re
import statistics
from pathlib import Path

TURN_SPLIT = re.compile(
    r"(?m)^(.+)\n — \n(\d{2}/\d{2}/\d{4}, \d{2}:\d{2})\n"
)
KEYBOARD_SMASH = re.compile(r"(?i)(?:[aeiou]{0,2}[sdfghjklçp]{5,}|k{4,}|w{3,}|h{3,})")
LAUGH = re.compile(r"(?i)\b(?:k{3,}|rs+|kkk+|haha+|hehe+|lol)\b")
EMOJI = re.compile(
    r"[\U0001F300-\U0001FAFF\U00002700-\U000027BF]|[><]:[()cC]|t-t|:c|:C|\><|uwu|grr",
    re.IGNORECASE,
)


def parse_turns(text: str, speaker: str) -> list[tuple[str, str]]:
    parts = TURN_SPLIT.split(text)
    turns: list[tuple[str, str]] = []
    i = 1
    while i + 2 < len(parts):
        name, time, body = parts[i].strip(), parts[i + 1], parts[i + 2].strip()
        if name == speaker:
            cleaned = "\n".join(
                line for line in body.splitlines()
                if line.strip() and line.strip() not in {"Imagem", "N/A"}
            ).strip()
            if cleaned:
                turns.append((time, cleaned))
        i += 3
    return turns


def summarize(turns: list[tuple[str, str]]) -> dict[str, float | int]:
    bodies = [body for _, body in turns]
    lengths = [len(body) for body in bodies] or [0]
    smash = sum(1 for body in bodies if KEYBOARD_SMASH.search(body))
    laugh = sum(1 for body in bodies if LAUGH.search(body))
    emoji = sum(1 for body in bodies if EMOJI.search(body))
    lines = [len(body.splitlines()) for body in bodies] or [1]
    return {
        "turns": len(bodies),
        "avg_chars": round(statistics.mean(lengths), 1),
        "median_chars": statistics.median(lengths),
        "avg_lines": round(statistics.mean(lines), 2),
        "smash_rate": round(smash / max(len(bodies), 1), 3),
        "laugh_rate": round(laugh / max(len(bodies), 1), 3),
        "emoji_rate": round(emoji / max(len(bodies), 1), 3),
    }


def write_overlay(out: Path, stats: dict[str, float | int], turns: list[tuple[str, str]]) -> None:
    out.mkdir(parents=True, exist_ok=True)
    (out / "extracted_turns.txt").write_text(
        "\n\n".join(f"[{time}]\n{body}" for time, body in turns),
        encoding="utf-8",
    )
    (out / "stats.txt").write_text(
        "\n".join(f"{key}={value}" for key, value in stats.items()) + "\n",
        encoding="utf-8",
    )
    short = stats["median_chars"] < 80
    smashy = stats["smash_rate"] >= 0.08
    laughy = stats["laugh_rate"] >= 0.12
    (out / "persona.txt").write_text(
        "You are a close chat companion, not a formal assistant and not a fictional named character from a game.\n"
        "Answer in the same informal register as the extracted chat turns: short bursts, line breaks, typos left as-is.\n"
        "Never mention prompts, models, or being an AI. Do not use Markdown, bullet lists, or customer-service tone.\n"
        f"Typical reply length is about {stats['median_chars']} characters"
        + (" in one or two fragments." if short else ".")
        + (" Keyboard-smash laughter is part of the voice." if smashy else "")
        + (" Laugh tokens are common." if laughy else "")
        + "\nDo not copy private names, handles, or unique personal facts from the source transcript.\n",
        encoding="utf-8",
    )
    (out / "lore.txt").write_text(
        "Keep lore light. Prefer in-the-moment chat over a biography. "
        "Do not invent a tragic backstory. Recurring hobbies may appear only when the user brings them up.\n",
        encoding="utf-8",
    )
    (out / "emotion.txt").write_text(
        "Feelings show as teasing, mock-dramatic reactions, check-ins, and sudden softness after a joke. "
        "Do not flatten into polite calm. Do not stay in one exaggerated mood every turn.\n",
        encoding="utf-8",
    )
    (out / "style.txt").write_text(
        "Follow the overlay persona instead of any built-in original-character style guide. "
        "Match rhythm and mannerisms from the extracted turns without quoting private details.\n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Chat transcript path")
    parser.add_argument("--speaker", required=True, help="Exact speaker label on turn headers")
    parser.add_argument("--out", required=True, help="Private output directory")
    args = parser.parse_args()
    text = Path(args.input).read_text(encoding="utf-8", errors="replace")
    turns = parse_turns(text, args.speaker)
    if not turns:
        raise SystemExit(f"No turns found for speaker {args.speaker!r}.")
    stats = summarize(turns)
    write_overlay(Path(args.out), stats, turns)
    print(f"Wrote {len(turns)} turns to {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
