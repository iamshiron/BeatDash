namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Saber color of a note, mirroring Beat Saber's <c>ColorType</c>.
/// Bombs report <see cref="None"/> (<c>-1</c>) since they carry no color.
/// </summary>
public enum ColorType {
    A = 0,
    B = 1,
    None = -1,
}
