using System.Collections.Generic;

namespace Test1
{
    internal static class AuthConfig
    {
        internal const string AdminUsername = "admin";
        internal const string AdminPassword = "";
        internal const string UserUsername = "user";
        internal const string UserPassword = "";

        internal static readonly Dictionary<string, (string Password, string Role)> Users =
            new()
            {
                { AdminUsername, (AdminPassword, "Admin") },
                { UserUsername, (UserPassword, "User") }
            };
    }
}

