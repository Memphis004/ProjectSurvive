# Archived ADRs — read as examples, not as rules

These thirteen records were written for the game the template was cut from. They are
kept for their reasoning, and they are **not binding on the template or on a game
built from it**.

| ADR | Topic | Why it is still worth reading |
| --- | --- | --- |
| [0007](0007-battle-contract-and-asmdef-placement.md) | a contract seam and where it lives | how to put an interface between Application and a simulation without a layer inversion |
| [0009](0009-footprint-picture-format.md) | a grid shape authored as a picture string | making an invalid state unrepresentable in the data format |
| [0010](0010-item-local-effects-deferred.md) | a number with no consumer yet | why a value waits in `data/` instead of becoming a code constant |
| [0013](0013-weight-speed-penalty-curve.md) | a curve whose two numbers live in data | the shape of "data decides, code applies" |
| [0014](0014-backpack-editing-undo-and-placement-ownership.md) | commands, snapshot undo, cell ownership | a worked atomic-command surface |
| [0015](0015-same-line-synergy-kinds.md) | two rules that looked like one | resolving an ambiguity by evidence rather than by widening a rule |
| [0016](0016-run-settlement-and-shop-commit-boundaries.md) | settlement and commit boundaries | one settlement per floor, typed refusals, a guard that is not an id |
| [0017](0017-ecslite-battle-simulation.md) | an ECS simulation behind a contract | what an engine costs, and which numbers it needed from data |
| [0018](0018-battle-camera-projection.md) | a projection chosen by measurement | how a visual question is settled with numbers |
| [0019](0019-camp-screen-read-model-and-gesture-preview.md) | a read model and a dry-run preview | preview and command sharing one predicate |
| [0020](0020-bag-moves-carry-the-items-on-them.md) | a rigid assembly through one rotation map | a rule change expressed as one function instead of a special case |
| [0021](0021-merge-upgrade-choice.md) | an offer that is a pure function of state | a modal whose content is reproducible |
| [0023](0023-cooldown-state-on-the-battle-frame.md) | display state on a published frame | what belongs on a port a View may read |

**The template's own ADRs are 0001–0099** and live in [`../../../adr/`](../../../adr/).
A game's ADRs start at **0100** (AGENTS rule 12). If a game needs one of the ideas
above, it writes its own record — a link into this folder is not a decision.

These files keep their original cross-links, some of which point at documents in this
archive; links that left the archive are intentionally not maintained.
