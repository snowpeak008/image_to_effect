"""Compatibility CLI for W24 S0a formal-capture projection."""

try:
    from .s0a_projection import main
except ImportError:  # pragma: no cover - direct script execution
    from s0a_projection import main


if __name__ == "__main__":
    raise SystemExit(main(["project", *__import__("sys").argv[1:]]))
