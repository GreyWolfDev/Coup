using Microsoft.Win32;

namespace Database
{
    public class RegHelper
    {
        private static RegistryKey _key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey("SOFTWARE\\Coup");
        public static string GetRegValue(string key)
        {
            return _key.GetValue(key, "").ToString();
        }
        public static string DBConnString => GetRegValue("DBConnectionString");
    }
}
