"""Local XTTS v2 HTTP sidecar for Portuguese (and other cloned) speech.

Speaks the Coqui XTTS language code `pt` from every wav/mp3/ogg/flac in --refs.
GPT-SoVITS stays on :9880/:9881; this process binds :9882.
"""
from __future__ import annotations

import argparse
import io
import logging
import os
import sys
import wave
from pathlib import Path

os.environ.setdefault("PYTHONUTF8", "1")
os.environ.setdefault("PYTHONIOENCODING", "utf-8")
os.environ.setdefault("COQUI_TOS_AGREED", "1")

from fastapi import FastAPI, Request
from fastapi.responses import Response
import numpy as np
import uvicorn

AUDIO_SUFFIXES = {".wav", ".mp3", ".ogg", ".flac", ".m4a"}
LOG = logging.getLogger("xtts")


def collect_refs(path: str | None) -> list[str]:
    if not path:
        return []
    target = Path(path)
    if target.is_file() and target.suffix.lower() in AUDIO_SUFFIXES:
        return [str(target)]
    if not target.is_dir():
        return []
    files = [
        str(item)
        for item in sorted(target.iterdir())
        if item.is_file() and item.suffix.lower() in AUDIO_SUFFIXES
    ]
    return files


def encode_wav(samples, sample_rate: int) -> bytes:
    audio = np.asarray(samples, dtype=np.float32).reshape(-1)
    pcm = np.clip(audio * 32767.0, -32768, 32767).astype(np.int16)
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(int(sample_rate))
        wav.writeframes(pcm.tobytes())
    return buffer.getvalue()


def configure_logging(log_path: Path) -> None:
    log_path.parent.mkdir(parents=True, exist_ok=True)
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
        handlers=[
            logging.FileHandler(log_path, encoding="utf-8"),
            logging.StreamHandler(sys.stdout),
        ],
    )


def _load_audio_without_ffmpeg(audiopath, sampling_rate):
    """Load reference clips via soundfile so torchcodec/FFmpeg is not required."""
    import soundfile as sf
    import torch
    import torchaudio

    data, lsr = sf.read(audiopath, dtype="float32", always_2d=True)
    audio = torch.from_numpy(data.T)
    if audio.size(0) != 1:
        audio = torch.mean(audio, dim=0, keepdim=True)
    if lsr != sampling_rate:
        audio = torchaudio.functional.resample(audio, lsr, sampling_rate)
    audio.clip_(-1, 1)
    return audio


def load_tts(device: str):
    import torch
    from TTS.api import TTS
    import TTS.tts.models.xtts as xtts_mod

    xtts_mod.load_audio = _load_audio_without_ffmpeg
    if device == "cuda" and not torch.cuda.is_available():
        LOG.warning("CUDA was requested but is not available; using CPU.")
        device = "cpu"
    LOG.info("Loading XTTS v2 on %s.", device)
    try:
        tts = TTS("tts_models/multilingual/multi-dataset/xtts_v2").to(device)
    except Exception:
        if device != "cpu":
            LOG.exception("XTTS failed to load on %s; retrying on CPU.", device)
            device = "cpu"
            tts = TTS("tts_models/multilingual/multi-dataset/xtts_v2").to(device)
        else:
            raise
    LOG.info("XTTS v2 ready on %s.", device)
    return tts, device


def sample_rate_of(tts) -> int:
    synthesizer = getattr(tts, "synthesizer", None)
    rate = getattr(synthesizer, "output_sample_rate", None) if synthesizer is not None else None
    return int(rate or 24000)


def synthesize(tts, text: str, refs: list[str], language: str):
    speaker = refs if len(refs) > 1 else refs[0]
    wav = tts.tts(
        text=text,
        speaker_wav=speaker,
        language=language,
        split_sentences=True,
    )
    return wav


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("-a", default="127.0.0.1")
    parser.add_argument("-p", type=int, default=9882)
    parser.add_argument("--refs", default="")
    parser.add_argument("--device", default="cuda")
    parser.add_argument("--log", default="")
    args = parser.parse_args()

    root = Path(__file__).resolve().parent
    log_path = Path(args.log) if args.log else root / "logs" / "xtts.log"
    configure_logging(log_path)

    default_refs = collect_refs(args.refs)
    if not default_refs:
        LOG.error("No speaker reference audio found at %s", args.refs)
        return 1

    tts, device = load_tts(args.device)
    rate = sample_rate_of(tts)
    app = FastAPI()

    @app.get("/ready")
    def ready():
        return {
            "ok": True,
            "device": device,
            "language": "pt",
            "refs": default_refs,
            "sample_rate": rate,
        }

    @app.post("/tts")
    async def tts_endpoint(request: Request):
        try:
            body = await request.json()
        except Exception:
            return Response(content=b"invalid json", status_code=400)

        text = str(body.get("text") or "").strip()
        if not text:
            return Response(content=b"empty text", status_code=400)

        language = str(body.get("language") or "pt").strip() or "pt"
        speaker = body.get("speaker_wav") or body.get("ref_audio_path") or args.refs
        if isinstance(speaker, list):
            refs: list[str] = []
            for item in speaker:
                refs.extend(collect_refs(str(item)))
        else:
            refs = collect_refs(str(speaker))
        if not refs:
            refs = default_refs
        if not refs:
            return Response(content=b"no speaker reference", status_code=400)

        try:
            wav = synthesize(tts, text, refs, language)
            return Response(content=encode_wav(wav, rate), media_type="audio/wav")
        except Exception as exception:
            LOG.exception("XTTS synthesis failed")
            return Response(content=str(exception).encode("utf-8", "replace"), status_code=500)

    LOG.info("XTTS listening on %s:%s with %s ref(s).", args.a, args.p, len(default_refs))
    uvicorn.run(app, host=args.a, port=args.p, log_level="info")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
