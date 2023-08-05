using CredentialManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NasMapper
{
    public partial class FormMain : Form
    {
        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int WNetAddConnection2(ref NETRESOURCE netResource, string password, string username, int flags);
        [StructLayout(LayoutKind.Sequential)]
        private class NETRESOURCE
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }
        private static string MapNetworkDriveByApi(string path, string username, string password, string driveLettr)
        {
            NETRESOURCE netResource = new NETRESOURCE
            {
                dwType = 1, // RESOURCETYPE_DISK
                lpLocalName = driveLettr,
                lpRemoteName = path
            };
            int result = WNetAddConnection2(ref netResource, password, username, 0x00000001);
            return result == 0 ? "" : result.ToString();
        }

        private static string MapNetworkDriveByCmd(string path, string username, string password, string driveLettr)
        {            
            //ProcessStartInfo processStartInfo = new ProcessStartInfo("net", $@"use {driveLettr} {path} ""{password}"" /user:""{username}"" /PERSISTENT:YES");
            ProcessStartInfo processStartInfo = new ProcessStartInfo("net", $@"use {driveLettr} {path} /SAVECRED /PERSISTENT:YES");
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            var process = Process.Start(processStartInfo);
            //process.EnableRaisingEvents = true;
            //process.BeginErrorReadLine();
            //process.BeginOutputReadLine();
            var result = "";
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLineAsync().Result;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
            }
            while (!process.StandardError.EndOfStream)
            {
                var line = process.StandardError.ReadLineAsync().Result;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                result = line;
            }
            process.WaitForExit();
            process.Close();
            process.Dispose();
            return result;
        }

        public static void Uninstall()
        {
            foreach(var drive in drives)
            {
                RemoveNetworkDriveByCmd(drive);
            }
        }

        private static string RemoveNetworkDriveByCmd(string driveLetter)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo("net", $@"use {driveLetter} /delete");
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            var process = Process.Start(processStartInfo);
            //process.EnableRaisingEvents = true;
            //process.BeginErrorReadLine();
            //process.BeginOutputReadLine();
            var result = "";
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLineAsync().Result;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
            }
            while (!process.StandardError.EndOfStream)
            {
                var line = process.StandardError.ReadLineAsync().Result;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                result = line;
            }
            process.WaitForExit();
            process.Close();
            process.Dispose();
            return result;
        }

        private static void SaveCredentialByApi(string name, string path, string username, string password)
        {
            using (var cred = new Credential())
            {
                if (path.Length > 2)
                {
                    var startFlag = @"\\";
                    var startIndex = path.IndexOf(startFlag) + startFlag.Length;
                    if (startIndex < 2)
                    {
                        startIndex = 0;
                    }
                    var endIndex = path.IndexOf(@"\", startIndex);
                    if (endIndex < 0)
                    {
                        endIndex = path.Length;
                    }
                    cred.Target = path.Substring(startIndex, endIndex - startIndex);
                }
                else
                {
                    cred.Target = path;
                }

                cred.Username = username;
                cred.Password = password;
                cred.Type = CredentialType.DomainPassword;
                cred.PersistanceType = PersistanceType.Enterprise;
                cred.Description = name;
                cred.Save();
            }
        }

        private static void SaveCredentialByCmd(string name, string username, string password)
        {
            name = name.Replace("/", @"\");
            ProcessStartInfo processStartInfo = new ProcessStartInfo("CMDKEY", $@"/add:{name} /user:{username} /pass:{password}");
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            var process = Process.Start(processStartInfo);
            process.EnableRaisingEvents = true;
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();
            process.Close();
            process.Dispose();
        }

        private static Credential GetCredentialByApi(string name)
        {
            using (var cred = new Credential())
            {
                cred.Target = name;                
                if (cred.Load())
                {
                    return cred;
                }
                else
                {
                    return null;
                }
            }
        }

        private List<char> driveLetters;
        private static List<string> drives;
        private static IniHelper ini = null;

        public FormMain()
        {
            InitializeComponent();
        }

        static FormMain()
        {
            drives = new List<string>();
            ini = new IniHelper(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini"));
            drives.AddRange(ini.Read("Drives", "List").Split(','));
            drives.RemoveAll(item => string.IsNullOrWhiteSpace(item));
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            driveLetters = new List<char>();
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            for (var i = 2; i < 26; i++)
            {
                var driveLetter = (char)((byte)'A' + i);
                if (null == allDrives.FirstOrDefault(item => item.Name[0] == driveLetter))
                {
                    driveLetters.Add(driveLetter);
                }
            }
        }

        private void btnMap_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please input the NAS Name");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                MessageBox.Show("Please input the Username");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Please input the Passowrd");
                return;
            }
            if (driveLetters.Count == 0)
            {
                MessageBox.Show("No available drive letter");
                return;
            }
            var driver = driveLetters.FirstOrDefault();
            var driverLetter = $"{driver}:";
            string name = txtName.Text.Trim(), path = $@"\\{name}";
            //if (null == cbbDirveLetter.SelectedItem)
            //{
            //    MessageBox.Show("No available drive letter");
            //    return;
            //}
            //string name = null, path = txtPath.Text.Trim(),driverLetter = $"{cbbDirveLetter.SelectedItem.ToString()}:";
            //if (path.Length > 2)
            //{
            //    var startFlag = @"\\";
            //    var startIndex = path.IndexOf(startFlag) + startFlag.Length;
            //    if (startIndex < 2)
            //    {
            //        startIndex = 0;
            //    }
            //    var endIndex = path.IndexOf(@"\", startIndex);
            //    if (endIndex < 0)
            //    {
            //        endIndex = path.Length;
            //    }
            //    name = path.Substring(startIndex, endIndex - startIndex);
            //}
            //else
            //{
            //    name = path;
            //}
            SaveCredentialByCmd(name, txtUsername.Text.Trim(), txtPassword.Text.Trim());            
            var result = MapNetworkDriveByCmd(path, txtUsername.Text.Trim(), txtPassword.Text.Trim(), driverLetter);
            if (!string.IsNullOrEmpty(result))
            {
                MessageBox.Show($"Mapped failed: {result}");
            }
            else
            {
                driveLetters.Remove(driver);
                drives.Add(driverLetter);
                ini.Write("Drives", "List", string.Join(",", drives));
                MessageBox.Show($"Mapped successfully");
                txtName.Text = "";
                txtUsername.Text = "";
                txtPassword.Text = "";
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (drives.Count == 0)
            {
                MessageBox.Show("You may not exit unless at least one network drive is mapped");
                e.Cancel = true;
            }
        }
    }
}