"""Install a separate XTTS venv next to the GPT-SoVITS runtime.

Reuses the bundled Windows Python / CUDA torch via --system-site-packages so
the SoVITS environment is not modified. Safe to re-run.
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path


def run(command: list[str], env: dict[str, str] | None = None) -> None:
    print("+", " ".join(command), flush=True)
    subprocess.check_call(command, env=env)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime", required=True, help="voice-runtime directory")
    args = parser.parse_args()

    runtime = Path(args.runtime).resolve()
    bundled_python = runtime / "python" / "Scripts" / "python.exe"
    if not bundled_python.is_file():
        raise FileNotFoundError(f"Bundled Python not found: {bundled_python}")

    venv = runtime / "xtts-env"
    venv_python = venv / "Scripts" / "python.exe"
    uv = runtime / "uv.exe"
    if not venv_python.is_file():
        run([str(bundled_python), "-m", "venv", "--system-site-packages", str(venv)])

    env = os.environ.copy()
    env["COQUI_TOS_AGREED"] = "1"
    env["PYTHONUTF8"] = "1"
    env["PYTHONIOENCODING"] = "utf-8"

    if uv.is_file():
        run(
            [
                str(uv),
                "pip",
                "install",
                "--python",
                str(venv_python),
                "-r",
                str(Path(__file__).resolve().parent / "requirements-xtts.txt"),
            ],
            env,
        )
    else:
        run([str(venv_python), "-m", "pip", "install", "-U", "pip"], env)
        run(
            [
                str(venv_python),
                "-m",
                "pip",
                "install",
                "-r",
                str(Path(__file__).resolve().parent / "requirements-xtts.txt"),
            ],
            env,
        )

    print("XTTS environment is ready at", venv, flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
