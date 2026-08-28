"""Compatibility CLI for W24 S0a blind-review calibration statistics."""

from s0a_calibration import main


if __name__ == "__main__":
    raise SystemExit(main(["score", *__import__("sys").argv[1:]]))
