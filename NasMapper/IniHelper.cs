using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NasMapper
{
    public class IniHelper
    {
        private string strIniPath;

        public string IniPath
        {
            get { return strIniPath; }
        }

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public IniHelper(string iniPath)
        {
            // 
            // TODO: Add constructor logic here 
            // 
            this.strIniPath = iniPath;
        }

        public string Read(string Section, string Key)
        {
            StringBuilder temp = new StringBuilder(255);
            int i = GetPrivateProfileString(Section, Key, "", temp, 255, this.strIniPath);
            return temp.ToString();
        }

        public void Write(string Section, string Key, string Value)
        {
            WritePrivateProfileString(Section, Key, Value, this.strIniPath);
        }
    }
}