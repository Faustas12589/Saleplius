namespace SalePlius.Client.Services;

/// <summary>Labels shared by the program pages.</summary>
public static class ProgramText
{
    public static readonly string[] DayNames =
        { "Pirmadienis", "Antradienis", "Trečiadienis", "Ketvirtadienis", "Penktadienis", "Šeštadienis", "Sekmadienis" };

    public static readonly string[] DayShort = { "Pr", "An", "Tr", "Kt", "Pn", "Št", "Sk" };

    public static readonly string[] Goals = { "Jėga", "Raumenų masė", "Ištvermė", "Svorio metimas", "Bendras" };

    public static string Day(int day) => day is >= 1 and <= 7 ? DayNames[day - 1] : $"{day} diena";

    public static string DayAbbr(int day) => day is >= 1 and <= 7 ? DayShort[day - 1] : day.ToString();
}
