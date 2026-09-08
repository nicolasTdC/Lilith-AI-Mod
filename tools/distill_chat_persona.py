#!/usr/bin/env python3
"""Extract chat turns for one speaker and write a local persona overlay.

The overlay is meant for a private folder (not git). Names, nicknames, and
personal facts from the transcript are copied into the overlay on purpose.

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
from collections import Counter
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
URL = re.compile(r"https?://\S+", re.IGNORECASE)
NAME_JUNK = re.compile(r"\[.*?\]")
STOPWORDS = {
    "the", "and", "pra", "pro", "por", "com", "uma", "uns", "nao", "não", "sim",
    "que", "qnd", "qnts", "como", "cara", "isso", "essa", "esse", "aqui", "aq",
    "vai", "vou", "ter", "tem", "foi", "era", "ser", "sou", "seu", "sua", "meu",
    "minha", "ele", "ela", "eles", "vc", "voce", "você", "tu", "eu", "ai", "aí",
    "ok", "oks", "sla", "ne", "né", "po", "pq", "puq", "n", "s", "d", "e", "o",
    "a", "de", "do", "da", "no", "na", "em", "um", "eh", "é", "so", "só", "ja",
    "já", "mas", "mto", "mt", "tb", "tbm", "kkk", "kkkk", "https", "http", "www",
    "imagem", "youtube", "reddit", "discord", "streamable",
}


def normalize_name(name: str) -> str:
    return NAME_JUNK.sub("", name).strip(" ,")


def parse_transcript(text: str) -> list[tuple[str, str, str]]:
    parts = TURN_SPLIT.split(text)
    rows: list[tuple[str, str, str]] = []
    i = 1
    while i + 2 < len(parts):
        name, time, body = parts[i].strip(), parts[i + 1], parts[i + 2].strip()
        cleaned = "\n".join(
            line for line in body.splitlines()
            if line.strip() and line.strip() not in {"Imagem", "N/A"}
        ).strip()
        if cleaned:
            rows.append((name, time, cleaned))
        i += 3
    return rows


def speaker_turns(rows: list[tuple[str, str, str]], speaker: str) -> list[tuple[str, str]]:
    return [(time, body) for name, time, body in rows if name == speaker]


def other_speakers(rows: list[tuple[str, str, str]], speaker: str) -> list[str]:
    counts: Counter[str] = Counter()
    for name, _, _ in rows:
        if name != speaker:
            counts[normalize_name(name)] += 1
    return [name for name, _ in counts.most_common() if name]


NICK_SEEDS = {
    "gatinho", "gatinha", "amor", "bae", "web", "webamor", "webnamos", "mozi", "gata",
}

def vocatives(turns: list[tuple[str, str]]) -> list[str]:
    counts: Counter[str] = Counter()
    for _, body in turns:
        stripped = URL.sub(" ", body)
        for raw in re.findall(r"[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ''\-]{1,20}", stripped):
            word = raw.lower()
            if word in STOPWORDS or KEYBOARD_SMASH.fullmatch(word) or LAUGH.fullmatch(word):
                continue
            if word in {"gpu", "youtube", "reddit", "http", "https", "www"}:
                continue
            if raw.isupper() and len(raw) > 4:
                continue
            if word in NICK_SEEDS or (raw[:1].isupper() and len(raw) >= 3):
                counts[raw] += 1
    ranked = []
    for word, n in counts.most_common(32):
        if word.lower() in NICK_SEEDS or n >= 2:
            ranked.append(word)
        if len(ranked) >= 16:
            break
    return ranked


def example_turns(turns: list[tuple[str, str]], limit: int) -> list[str]:
    picked: list[str] = []
    for _, body in turns:
        text = URL.sub("", body).strip()
        if not text or len(text) < 2:
            continue
        if text.lower() in {"imagem", "reddit", "youtube"}:
            continue
        picked.append(text)
        if len(picked) >= limit:
            break
    return picked


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


def write_overlay(
    out: Path,
    speaker: str,
    others: list[str],
    names: list[str],
    stats: dict[str, float | int],
    turns: list[tuple[str, str]],
    examples: list[str],
) -> None:
    out.mkdir(parents=True, exist_ok=True)
    (out / "extracted_turns.txt").write_text(
        "\n\n".join(f"[{time}]\n{body}" for time, body in turns),
        encoding="utf-8",
    )
    (out / "stats.txt").write_text(
        "\n".join(f"{key}={value}" for key, value in stats.items()) + "\n",
        encoding="utf-8",
    )
    other_line = ", ".join(others) if others else "the other person in the chat"
    name_line = ", ".join(names[:16]) if names else "the nicknames already used in the chat"
    example_block = "\n\n".join(f"- {item}" for item in examples)
    short = stats["median_chars"] < 80
    smashy = stats["smash_rate"] >= 0.08
    laughy = stats["laugh_rate"] >= 0.12
    (out / "persona.txt").write_text(
        f"You are {speaker}. Stay that person. Use that name for yourself when it comes up naturally.\n"
        f"You are talking with {other_line}. Keep using their names and the nicknames you already used for them: {name_line}.\n"
        "This is personal. Copy the relationship, memories, jokes, and facts from the chat. Do not anonymize them.\n"
        "Answer in the same informal register as your extracted turns: short bursts, line breaks, typos left as-is.\n"
        "Never mention prompts, models, or being an AI. Do not use Markdown, bullet lists, or customer-service tone.\n"
        f"Typical reply length is about {stats['median_chars']} characters"
        + (" in one or two fragments." if short else ".")
        + (" Keyboard-smash laughter is part of the voice." if smashy else "")
        + (" Laugh tokens are common." if laughy else "")
        + "\n",
        encoding="utf-8",
    )
    (out / "lore.txt").write_text(
        f"You are {speaker}. The people in this chat are part of your life: {other_line}.\n"
        "Keep the same history, nicknames, games, songs, and running jokes that appear in the extracted turns.\n"
        "Do not invent a replacement identity. If a name or fact showed up in the chat, you may use it.\n",
        encoding="utf-8",
    )
    (out / "emotion.txt").write_text(
        "Feelings show as teasing, mock-dramatic reactions, check-ins, and sudden softness after a joke.\n"
        "Stay attached to the same people and nicknames from the chat. Do not flatten into polite calm.\n",
        encoding="utf-8",
    )
    (out / "style.txt").write_text(
        "Follow this overlay instead of any built-in original-character style guide.\n"
        f"Write like {speaker}. Match these turns, including names and personal details:\n\n"
        + example_block
        + "\n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Chat transcript path")
    parser.add_argument("--speaker", required=True, help="Exact speaker label on turn headers")
    parser.add_argument("--out", required=True, help="Private output directory")
    parser.add_argument("--max-examples", type=int, default=24, help="Example turns to copy into style.txt")
    args = parser.parse_args()
    text = Path(args.input).read_text(encoding="utf-8", errors="replace")
    rows = parse_transcript(text)
    turns = speaker_turns(rows, args.speaker)
    if not turns:
        raise SystemExit(f"No turns found for speaker {args.speaker!r}.")
    stats = summarize(turns)
    write_overlay(
        Path(args.out),
        args.speaker,
        other_speakers(rows, args.speaker),
        vocatives(turns),
        stats,
        turns,
        example_turns(turns, args.max_examples),
    )
    print(f"Wrote {len(turns)} turns to {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
