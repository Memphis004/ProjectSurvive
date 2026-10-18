# Gotcha — a published frame only survives until the next write

`Shared/PrevCurBuffer<T>` (ADR 0012) publishes a frame by swapping two slots that are
**reused in place**. A reader's span is valid until the next `Publish`; reading it
after another `Step` renders memory that is being overwritten, and the corruption is
frame-rate dependent rather than reproducible.

The fixed-step loop therefore does this, in this order:

1. accumulate frame time into whole ticks;
2. for each tick: `Step()` the session, then **copy** the published frame into a
   snapshot the view owns;
3. render/interpolate from the snapshot with the accumulator's leftover alpha.

The copy belongs inside the frame that stepped, not in `Update` after the loop: a
stall that drops several ticks must leave the view holding the newest frame, not a
slot from the middle of the burst. Copy into arrays allocated once — the copy of a
warmed, pre-sized buffer allocates nothing.

**Symptom to recognise:** positions jitter or move backwards only under load, and a
test that steps and reads in the same call is green. That is a reader holding a live
span across a step, not a simulation bug.
