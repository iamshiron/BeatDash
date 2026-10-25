namespace Shiron.BeatDash.DB;

/// <summary>SQLite built-ins for expressions the SQLite provider can't translate natively. Only valid in SQLite LINQ queries.</summary>
public static class SqliteDbFunctions {
    /// <summary>SQLite's <c>julianday()</c>: fractional days since the Julian epoch, so differences are elapsed time.</summary>
    public static double JulianDay(DateTime value) => throw new NotSupportedException("JulianDay is only translatable in SQLite queries.");
}
