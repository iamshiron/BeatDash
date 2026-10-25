using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Shiron.BeatDash.DB;

/// <summary>Marks timestamps read from engines without time zone support as UTC.</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
