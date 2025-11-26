using System.Collections.Generic;

namespace Test1
{
    internal static class AuthConfig
    {
        internal const string AdminUsername = "admin";
        internal const string AdminPassword = "1234";
        internal const string UserUsername = "user";
        internal const string UserPassword = "4321";

        internal static readonly Dictionary<string, (string Password, string Role)> Users =
            new()
            {
                { AdminUsername, (AdminPassword, "Admin") },
                { UserUsername, (UserPassword, "User") }
            };
    }
}

