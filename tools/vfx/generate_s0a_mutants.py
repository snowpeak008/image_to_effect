"""Compatibility CLI for the parameterized W24 S0a mutant generator."""

from s0a_calibration import main


if __name__ == "__main__":
    raise SystemExit(main(["generate", *__import__("sys").argv[1:]]))
