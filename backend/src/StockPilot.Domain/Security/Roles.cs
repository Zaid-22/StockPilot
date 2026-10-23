namespace StockPilot.Domain.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Viewer = "Viewer";
    public static IReadOnlyList<string> All { get; } = [Admin, Staff, Viewer];
}
