using System.Collections.Generic;

namespace Test1
{
    internal static class AuthConfig
    {
        
        internal static readonly Dictionary<string, string> MachineRoles = new()
    {
        { "MSI", "Admin" },
        { "SAAIBUCIM003", "Admin" }
        
    };
        internal const string AdminPassword = "";
        internal static string GetRole(string machineName)
        {
            return MachineRoles.TryGetValue(machineName, out var role) ? role : "User";
        }
    }

}

