namespace ClothingStore.Core.Licensing;

public enum LicenseState
{
    /// <summary>Licensing isn't set up in this build (developer build without a public key).</summary>
    NotConfigured,
    Missing,
    Invalid,
    WrongMachine,
    Valid,
    ExpiringSoon,
    Expired,
}

public sealed record LicenseStatus(LicenseState State, LicenseData? License = null, int DaysLeft = 0)
{
    /// <summary>Days before expiry when the warning starts.</summary>
    public const int WarningDays = 30;

    public bool CanRun => State is LicenseState.Valid or LicenseState.ExpiringSoon or LicenseState.NotConfigured;

    /// <summary>
    /// Checks a key for this PC. <paramref name="today"/> should be the latest trustworthy date known (see
    /// <see cref="TrustedToday"/>), so setting the PC's clock back doesn't extend the license.
    /// </summary>
    public static LicenseStatus Evaluate(string? key, System.Security.Cryptography.ECDsa? publicKey, string machineId, DateOnly today)
    {
        if (publicKey is null) return new LicenseStatus(LicenseState.NotConfigured);
        if (string.IsNullOrWhiteSpace(key)) return new LicenseStatus(LicenseState.Missing);

        var license = LicenseCodec.Verify(key, publicKey);
        if (license is null) return new LicenseStatus(LicenseState.Invalid);
        if (!license.AllowsMachine(machineId)) return new LicenseStatus(LicenseState.WrongMachine, license);

        var daysLeft = license.ExpiresOn.DayNumber - today.DayNumber;
        if (daysLeft < 0) return new LicenseStatus(LicenseState.Expired, license, daysLeft);
        return new LicenseStatus(daysLeft <= WarningDays ? LicenseState.ExpiringSoon : LicenseState.Valid, license, daysLeft);
    }

    /// <summary>
    /// The latest of: this PC's clock, the database server's clock, the latest date this app has seen before, and the
    /// latest sale in the database. Turning the PC's clock back can't make the date earlier than any of the others.
    /// </summary>
    public static DateOnly TrustedToday(DateTime localNow, params DateTime?[] others)
    {
        var latest = others.Where(d => d is not null).Select(d => d!.Value).Append(localNow).Max();
        return DateOnly.FromDateTime(latest);
    }
}
