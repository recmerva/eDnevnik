namespace backend.Models;

public static class Roles
{
    public const string Parent = "Roditelj";
    public const string Teacher = "Nastavnik";
    public const string Administrator = "Administrator";
    public const string Staff = Administrator + "," + Teacher;
}
