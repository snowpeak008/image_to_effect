"""Pure-Python W24 diagnostic-render measurement utilities.

The package deliberately measures only diagnostic buffers.  It has no concept
of visual quality, references, or production acceptance.
"""

from .render_metrics import EvidenceInvalid, run_report

__all__ = ["EvidenceInvalid", "run_report"]
