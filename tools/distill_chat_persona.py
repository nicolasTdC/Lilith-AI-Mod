#!/usr/bin/env python3
"""Extract chat turns for one or more speakers and write a local persona overlay.

The overlay is meant for a private folder (not git). Names, nicknames, and
personal facts from the transcript are copied into the overlay on purpose.

Expected transcript shape (WhatsApp/Google Chat style):

    SpeakerName
     —
    DD/MM/YYYY, HH:MM
    message text

An optional second header line is allowed (Discord/Google nick decoration):

    SpeakerName
     [>ᆺ<],
     —
    DD/MM/YYYY, HH:MM
    message text

Usage:
    python tools/distill_chat_persona.py \\
        --input /path/to/chat1.txt --speaker "SpeakerA" \\
        --input /path/to/chat2.txt --speaker "SpeakerB" \\
        --name "DisplayName" \\
        --out /path/to/private/persona
"""
from __future__ import annotations

import argparse
import re
import statistics
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

TURN_SPLIT = re.compile(
    r"(?m)^(.+)\n(?:[ \t]*\[[^\]]+\][ \t]*,?[ \t]*\n)? — \n(\d{2}/\d{2}/\d{4}, \d{2}:\d{2})\n"
)
KEYBOARD_SMASH = re.compile(r"(?i)(?:[aeiou]{0,2}[sdfghjklçp]{5,}|k{4,}|w{3,}|h{3,})")
LAUGH = re.compile(r"(?i)\b(?:k{3,}|rs+|kkk+|haha+|hehe+|lol)\b")
EMOJI = re.compile(
    r"[\U0001F300-\U0001FAFF\U00002700-\U000027BF]|[><]:[()cC]|t-t|:c|:C|\><|uwu|grr",
    re.IGNORECASE,
)
URL = re.compile(r"https?://\S+", re.IGNORECASE)
NAME_JUNK = re.compile(r"\[.*?\]")
SYSTEM_SPEAKER = re.compile(r"(?i)chamada|iniciou uma|started a call|added|removed")
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


NICK_SEEDS = {
    "gatinho", "gatinha", "amor", "bae", "web", "webamor", "webnamos", "mozi", "gata",
}


@dataclass(frozen=True)
class Turn:
    speaker: str
    time: str
    body: str
    source: str


def normalize_name(name: str) -> str:
    return NAME_JUNK.sub("", name).strip(" ,")


def header_matches(header: str, speaker: str) -> bool:
    raw_h, raw_s = header.strip(), speaker.strip()
    if raw_h.casefold() == raw_s.casefold():
        return True
    norm_h, norm_s = normalize_name(raw_h), normalize_name(raw_s)
    if norm_h and norm_s and norm_h.casefold() == norm_s.casefold():
        return True
    return False


def parse_transcript(text: str, source: str = "") -> list[Turn]:
    parts = TURN_SPLIT.split(text)
    rows: list[Turn] = []
    i = 1
    while i + 2 < len(parts):
        name, time, body = parts[i].strip(), parts[i + 1], parts[i + 2].strip()
        cleaned = "\n".join(
            line for line in body.splitlines()
            if line.strip() and line.strip() not in {"Imagem", "N/A"}
        ).strip()
        if cleaned:
            rows.append(Turn(name, time, cleaned, source))
        i += 3
    return rows


def speaker_turns(rows: list[Turn], speaker: str) -> list[Turn]:
    matched = [row for row in rows if header_matches(row.speaker, speaker)]
    if matched:
        return matched
    headers = sorted({row.speaker for row in rows})
    raise SystemExit(
        f"No turns found for speaker {speaker!r} in {rows[0].source if rows else 'input'}. "
        f"Headers: {headers}"
    )


def is_system_speaker(name: str) -> bool:
    cleaned = normalize_name(name)
    if not cleaned or len(cleaned) > 40:
        return True
    return bool(SYSTEM_SPEAKER.search(name) or SYSTEM_SPEAKER.search(cleaned))


def other_speakers(rows: list[Turn], self_labels: set[str]) -> list[str]:
    counts: Counter[str] = Counter()
    for row in rows:
        if any(header_matches(row.speaker, label) for label in self_labels):
            continue
        if is_system_speaker(row.speaker):
            continue
        cleaned = normalize_name(row.speaker)
        if cleaned:
            counts[cleaned] += 1
    return [name for name, _ in counts.most_common() if name]


def vocatives(turns: list[Turn], extra_stop: set[str], others: list[str]) -> list[str]:
    counts: Counter[str] = Counter()
    blocked = {word.casefold() for word in extra_stop}
    allowed = set(NICK_SEEDS)
    for other in others:
        token = re.split(r"\d+", other, maxsplit=1)[0].strip("_- ").lower()
        if len(token) >= 3:
            allowed.add(token)
            allowed.add(other.lower())
    for turn in turns:
        stripped = URL.sub(" ", turn.body)
        for raw in re.findall(r"[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ''\-]{1,20}", stripped):
            word = raw.lower()
            if word in blocked or word in STOPWORDS:
                continue
            if KEYBOARD_SMASH.fullmatch(word) or LAUGH.fullmatch(word):
                continue
            if word in allowed:
                counts[word] += 1
    return [word for word, _ in counts.most_common(16)]


def is_style_noise(text: str) -> bool:
    stripped = URL.sub(" ", text)
    stripped = LAUGH.sub(" ", stripped)
    stripped = KEYBOARD_SMASH.sub(" ", stripped)
    stripped = EMOJI.sub(" ", stripped)
    letters = re.sub(r"\W+", "", stripped, flags=re.UNICODE)
    return len(letters) < 2


def example_turns(turns: list[Turn], limit: int) -> list[str]:
    grouped: dict[str, list[Turn]] = defaultdict(list)
    for turn in turns:
        grouped[turn.source or "_"].append(turn)
    queues = [list(items) for _, items in sorted(grouped.items())]
    picked: list[str] = []
    seen: set[str] = set()
    while queues and len(picked) < limit:
        next_queues: list[list[Turn]] = []
        for queue in queues:
            while queue:
                turn = queue.pop(0)
                text = URL.sub("", turn.body).strip()
                if not text or len(text) < 2:
                    continue
                if text.lower() in {"imagem", "reddit", "youtube"}:
                    continue
                if is_style_noise(text):
                    continue
                key = re.sub(r"\s+", " ", text).casefold()
                if key in seen:
                    continue
                seen.add(key)
                picked.append(text)
                break
            if queue:
                next_queues.append(queue)
            if len(picked) >= limit:
                break
        queues = next_queues
    return picked


def parse_turn_time(value: str) -> datetime:
    try:
        return datetime.strptime(value, "%d/%m/%Y, %H:%M")
    except ValueError:
        return datetime.min


def summarize(turns: list[Turn]) -> dict[str, float | int]:
    bodies = [turn.body for turn in turns]
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
    display_name: str,
    aliases: list[str],
    others: list[str],
    names: list[str],
    stats: dict[str, float | int],
    turns: list[Turn],
    examples: list[str],
) -> None:
    out.mkdir(parents=True, exist_ok=True)
    ordered = sorted(turns, key=lambda turn: (parse_turn_time(turn.time), turn.source, turn.time))
    (out / "extracted_turns.txt").write_text(
        "\n\n".join(f"[{turn.time}]\n{turn.body}" for turn in ordered),
        encoding="utf-8",
    )
    (out / "stats.txt").write_text(
        "\n".join(f"{key}={value}" for key, value in stats.items()) + "\n",
        encoding="utf-8",
    )
    other_line = ", ".join(others) if others else "the other person in the chat"
    name_line = ", ".join(names[:16]) if names else "the nicknames already used in the chat"
    alias_line = ""
    extra = [item for item in aliases if item.casefold() != display_name.casefold()]
    if extra:
        alias_line = f"You also go by {', '.join(extra)}; those are still you.\n"
    example_block = "\n\n".join(f"- {item}" for item in examples)
    short = stats["median_chars"] < 80
    smashy = stats["smash_rate"] >= 0.08
    laughy = stats["laugh_rate"] >= 0.12
    (out / "persona.txt").write_text(
        f"You are {display_name}. Stay that person. Use that name for yourself when it comes up naturally.\n"
        f"{alias_line}"
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
        f"You are {display_name}. The people in this chat are part of your life: {other_line}.\n"
        + (f"You have also used the names {', '.join(extra)}.\n" if extra else "")
        + "Keep the same history, nicknames, games, songs, and running jokes that appear in the extracted turns.\n"
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
        f"Write like {display_name}. Match these turns, including names and personal details:\n\n"
        + example_block
        + "\n",
        encoding="utf-8",
    )


def paired_sources(inputs: list[str], speakers: list[str]) -> list[tuple[Path, str]]:
    if len(speakers) == 1:
        return [(Path(path), speakers[0]) for path in inputs]
    if len(speakers) != len(inputs):
        raise SystemExit("Pass one --speaker, or one --speaker per --input.")
    return list(zip((Path(path) for path in inputs), speakers, strict=True))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", action="append", required=True, help="Chat transcript path (repeatable)")
    parser.add_argument(
        "--speaker",
        action="append",
        required=True,
        help="Exact speaker label on turn headers. Repeat once per --input, or once for every file.",
    )
    parser.add_argument("--name", default="", help="Persona name written into the overlay (default: speaker)")
    parser.add_argument("--out", required=True, help="Private output directory")
    parser.add_argument("--max-examples", type=int, default=36, help="Example turns to copy into style.txt")
    args = parser.parse_args()

    sources = paired_sources(args.input, args.speaker)
    all_rows: list[Turn] = []
    turns: list[Turn] = []
    matched_labels: list[str] = []
    for path, speaker in sources:
        text = path.read_text(encoding="utf-8", errors="replace")
        rows = parse_transcript(text, source=str(path))
        if not rows:
            raise SystemExit(f"No turns parsed from {path}.")
        all_rows.extend(rows)
        selected = speaker_turns(rows, speaker)
        turns.extend(selected)
        label = selected[0].speaker
        if all(not header_matches(existing, label) for existing in matched_labels):
            matched_labels.append(label)

    display_name = args.name.strip() or matched_labels[0]
    stats = summarize(turns)
    stop = {display_name, *matched_labels}
    others = other_speakers(all_rows, set(matched_labels))
    write_overlay(
        Path(args.out),
        display_name,
        matched_labels,
        others,
        vocatives(turns, stop, others),
        stats,
        turns,
        example_turns(turns, args.max_examples),
    )
    print(f"Wrote {len(turns)} turns as {display_name!r} to {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
