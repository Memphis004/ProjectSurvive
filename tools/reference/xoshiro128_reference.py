#!/usr/bin/env python3
"""Independent reference generator for the Core randomness primitives.

Why this file exists
--------------------
``lib/Tests~/Core.Tests`` pins golden vectors for ``Xoshiro128`` and
``RandomStreams``. Those numbers must not come from the C# code they are
supposed to check, or the test would only prove self-consistency. This script is
a second, independent implementation written from the algorithm description
(xoshiro128++ with splitmix32 seeding), used to produce and re-verify them.

Not used at build time. Run it to regenerate/confirm:

    python tools/reference/xoshiro128_reference.py

Compare the output with ``docs/stages/01-report.md``. If the two disagree, the
C# implementation drifted - fix the code (and the pinned vectors) deliberately,
never by copying values out of the C# output.
"""

MASK = 0xFFFFFFFF


def rotl(value, count):
    return ((value << count) | (value >> (32 - count))) & MASK


def splitmix32_next(state):
    """One splitmix32 step: add the golden-ratio odd constant, then mix."""
    state = (state + 0x9E3779B9) & MASK
    z = state
    z = ((z ^ (z >> 16)) * 0x21F0AAAD) & MASK
    z = ((z ^ (z >> 15)) * 0x735A2D97) & MASK
    return state, (z ^ (z >> 15)) & MASK


def seed_state(seed):
    """Expand a 32-bit seed into the four xoshiro128 state words."""
    state, words = seed & MASK, []
    for _ in range(4):
        state, word = splitmix32_next(state)
        words.append(word)
    if words == [0, 0, 0, 0]:
        words[0] = 0x9E3779B9
    return words


def next_uint(state):
    """xoshiro128++ output and state transition."""
    result = (rotl((state[0] + state[3]) & MASK, 7) + state[0]) & MASK
    t = (state[1] << 9) & MASK
    state[2] ^= state[0]
    state[3] ^= state[1]
    state[1] ^= state[2]
    state[0] ^= state[3]
    state[2] ^= t
    state[3] = rotl(state[3], 11)
    return result


def next_int(state, low, high):
    """Unbiased bounded generation (Lemire, multiply-shift with rejection)."""
    span = high - low
    while True:
        product = (next_uint(state) & MASK) * span
        low_bits = product & MASK
        if low_bits >= span:
            return low + (product >> 32)
        if low_bits >= ((0x100000000 % span) & MASK):
            return low + (product >> 32)


# Stream salts from lib/Runtime/Shared/RandomStreams.cs (shop / battle / visual).
STREAM_SALTS = {"shop": 0x53484F50, "battle": 0x42415454, "visual": 0x56495355}

# Mirrors ProjectSurvive.Sim defaults: 300 ticks at 30 Hz from 60 fps frames.
SIM_SEED = 20261013
SIM_TICKS = 300
SIM_FRAME_HZ = 60
SIM_TICK_HZ = 30
SIM_SHOP_INTERVAL = 30
FNV_OFFSET, FNV_PRIME = 2166136261, 16777619


def fnv1a(hash_value, value):
    for shift in (0, 8, 16, 24):
        hash_value = ((hash_value ^ ((value >> shift) & 0xFF)) * FNV_PRIME) & MASK
    return hash_value


def main():
    print("== Xoshiro128 first 8 outputs ==")
    for seed in (0, 1, 12345, 0xDEADBEEF):
        state = seed_state(seed)
        print("seed %-10d %s" % (seed, " ".join("0x%08Xu" % next_uint(state) for _ in range(8))))

    print("\n== RandomStreams.DeriveStreamSeed ==")
    for master in (0, 1, 12345, 0xFFFFFFFF):
        derived = {name: splitmix32_next((master & MASK) ^ salt)[1] for name, salt in STREAM_SALTS.items()}
        print("master %-10d %s" % (master, " ".join("%s=0x%08Xu" % (n, derived[n]) for n in STREAM_SALTS)))

    print("\n== NextInt(0, 100), seed 7 ==")
    state = seed_state(7)
    print(" ".join(str(next_int(state, 0, 100)) for _ in range(6)))

    print("\n== NextFloat(), seed 12345 (24-bit) ==")
    state = seed_state(12345)
    print(" ".join("%.10f" % ((next_uint(state) >> 8) * (1.0 / 16777216.0)) for _ in range(3)))

    print("\n== ProjectSurvive.Sim fingerprints (seed %d) ==" % SIM_SEED)
    streams = {name: seed_state(splitmix32_next((SIM_SEED & MASK) ^ salt)[1]) for name, salt in STREAM_SALTS.items()}
    battle = visual = shop = FNV_OFFSET
    sequence = FNV_OFFSET
    for tick in range(SIM_TICKS):
        battle_value = next_uint(streams["battle"])
        visual_value = next_uint(streams["visual"])
        battle = fnv1a(battle, battle_value)
        visual = fnv1a(visual, visual_value)
        sequence = fnv1a(fnv1a(sequence, battle_value), visual_value)
        if tick % SIM_SHOP_INTERVAL == 0:
            shop = fnv1a(shop, next_uint(streams["shop"]))
    print("shopDigest=0x%08x battleDigest=0x%08x visualDigest=0x%08x" % (shop, battle, visual))
    print("determinismDigest=0x%08x" % sequence)

    print("\n== FixedTicker: 600 frames of 1/60 at 30 Hz ==")
    tick_seconds, pending, total = 1.0 / SIM_TICK_HZ, 0.0, 0
    max_pending = 4 * tick_seconds
    for _ in range(600):
        pending += 1.0 / SIM_FRAME_HZ
        if pending > max_pending:
            pending = max_pending
        due = int((pending + 1e-9 * tick_seconds) / tick_seconds)
        if due > 0:
            pending -= due * tick_seconds
            if pending < 0:
                pending = 0.0
            total += due
    print("ticks=%d pending=%.20g" % (total, pending))


if __name__ == "__main__":
    main()
