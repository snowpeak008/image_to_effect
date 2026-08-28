# W24 visual QA and user report template

## Visual QA (independent reviewer)

- candidate identity and all frozen hashes
- one permitted route: `VISUAL_PASS`, `VISUAL_FAIL`, `EVIDENCE_INVALID`, `CONTRACT_AMBIGUOUS`, or `VISUAL_UNCERTAIN`
- for each visual requirement: frame interval, ROI/layer, contract statement, observation, pass/fail/uncertain
- if failing: concrete frame and location; if uncertain: the required routing class

## User signature (only user may complete)

- user decision: signed / rejected
- `contractRevision`, `buildHash`, `captureProfileHash`, preview scene, dynamic playback conditions
- review notes and any new cheat / requirement to preserve in the verdict corpus

No agent may fill the user decision as signed or label an unsigned entry L4.
