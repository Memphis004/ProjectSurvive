using System;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// The run's bia (เบี้ย) purse — the only place money changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Single-currency and run-scoped: a fresh <see cref="Wallet"/> starts at
    /// <c>economy.start_bia</c> when the run starts and dies with it (GDD: \"กลับ Title
    /// แล้วเริ่มใหม่ ไม่ใช้ seed เงิน ไอเทมของรอบก่อน\").
    /// </para>
    /// <para>
    /// <see cref="TrySpend"/> is atomic (AGENTS rule 7): either the whole amount
    /// leaves the purse or nothing does. <see cref="Credit"/> cannot fail — a
    /// reward always lands — and no arithmetic happens anywhere else, which is why
    /// a Presenter can show a price without ever computing one.
    /// </para>
    /// </remarks>
    public sealed class Wallet
    {
        /// <summary>Creates a purse holding <paramref name="startBia"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startBia"/> is negative.</exception>
        public Wallet(int startBia)
        {
            if (startBia < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startBia), startBia, "A wallet cannot start in debt.");
            }

            Bia = startBia;
        }

        /// <summary>Current balance, never negative.</summary>
        public int Bia { get; private set; }

        /// <summary>True when the purse holds at least <paramref name="amount"/>.</summary>
        public bool CanAfford(int amount) => amount >= 0 && Bia >= amount;

        /// <summary>
        /// Takes <paramref name="amount"/> out of the purse, or nothing at all.
        /// </summary>
        /// <returns>
        /// Success when the spend happened; otherwise a failure carrying
        /// <see cref="WalletError.InsufficientBia"/> (or
        /// <see cref="ProjectSurvive.Core.Shared.ErrorKind.Validation"/> for a
        /// negative amount) with the balance untouched.
        /// </returns>
        public Result TrySpend(int amount)
        {
            if (amount < 0)
            {
                return Result.Fail(WalletError.InsufficientBia, ErrorKind.Validation);
            }

            if (Bia < amount)
            {
                return Result.Fail(WalletError.InsufficientBia, ErrorKind.Insufficient);
            }

            Bia -= amount;
            return Result.Ok();
        }

        /// <summary>Adds a reward. A negative <paramref name="amount"/> is ignored, never a silent debt.</summary>
        public void Credit(int amount)
        {
            if (amount > 0)
            {
                Bia += amount;
            }
        }
    }
}
