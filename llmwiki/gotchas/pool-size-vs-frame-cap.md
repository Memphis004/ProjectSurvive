# Gotcha — pool size is per kind, the frame cap is per frame

A frame budget ("at most N entities rendered") and a pool ("K views pre-created per
kind") are different numbers, and making them equal is wrong. The game-era battle
pooled **1793** views against a `MaxRenderEntities` of **1024**: the pool had to be
the larger number, because a legal frame can ask for more views of one kind than the
per-frame total suggests (a wave of one enemy type, a burst of one projectile).

**What goes wrong when they are equal:** the pool runs dry mid-frame, and the view
either allocates (a hitch on a hot path, which AGENTS rule 8 forbids) or silently
skips an entity — a hole in the screen that no test catches unless it counts.

**The check that proves it:** count creations *after* warm-up. Warm the pool at scene
start, run a representative fight, and assert `CreatedAfterWarmup == 0` **and** that
the number of live views never exceeds the pool's capacity. A view that reports both
numbers can be tested; one that only reports "pooled" cannot.

**Where the numbers live:** capacities are a presentation decision, so they are
constants in the view — not balance numbers in `data/`. If a game tunes them per
floor, they become data and get a column.
