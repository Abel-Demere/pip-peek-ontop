// PIP Peek's on-demand native messaging component. No background service.
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

class PeekWindowHost {
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
    [DllImport("user32.dll", SetLastError=true)] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd, int index);
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    const string HostName = "com.pippeek.window";
    static string InstallDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PIP Peek", "Native"); } }
    static Dictionary<string,object> LoadJson(string path) {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream)) return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
    }
    static Dictionary<string,object> ObjectAt(Dictionary<string,object> obj, string key) {
        object value; return obj != null && obj.TryGetValue(key, out value) ? value as Dictionary<string,object> : null;
    }
    static HashSet<string> FindExtensions(string[] args) {
        var ids = new HashSet<string>();
        foreach (string arg in args) if (arg.StartsWith("--extension-id=")) {
            string id = arg.Substring(15); if (System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-p]{32}$")) ids.Add(id);
        }
        // Discover only installed PIP Peek metadata. No extension ID entry is needed.
        string userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data");
        if (Directory.Exists(userData)) foreach (string profile in Directory.GetDirectories(userData)) {
            string name = Path.GetFileName(profile);
            if (name != "Default" && !name.StartsWith("Profile ")) continue;
            foreach (string file in new[] { "Preferences", "Secure Preferences" }) try {
                var settings = ObjectAt(ObjectAt(LoadJson(Path.Combine(profile, file)), "extensions"), "settings");
                if (settings == null) continue;
                foreach (var entry in settings) {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(entry.Key, "^[a-p]{32}$")) continue;
                    var info = entry.Value as Dictionary<string,object>; if (info == null) continue;
                    var manifest = ObjectAt(info, "manifest");
                    object path;
                    if (manifest == null && info.TryGetValue("path", out path) && path is string) try { manifest = LoadJson(Path.Combine((string)path, "manifest.json")); } catch { }
                    object extensionName;
                    if (manifest != null && manifest.TryGetValue("name", out extensionName) && (string)extensionName == "PIP Peek") ids.Add(entry.Key);
                }
            } catch { /* Chrome may be writing this profile; try the other metadata file. */ }
        }
        // Previous development checkout, retained for existing installs. Discovery handles other paths.
        ids.Add("kfgpgmgfmfngilhkfobenhfhbekgphbo");
        return ids;
    }
    static void Install(string[] args) {
        var origins = FindExtensions(args).Select(id => "chrome-extension://" + id + "/").ToList();
        Directory.CreateDirectory(InstallDirectory);
        string target = Path.Combine(InstallDirectory, "PeekWindowHost.exe");
        string source = Process.GetCurrentProcess().MainModule.FileName;
        if (!String.Equals(source, target, StringComparison.OrdinalIgnoreCase)) File.Copy(source, target, true);
        string manifestPath = Path.Combine(InstallDirectory, HostName + ".json");
        if (File.Exists(manifestPath)) try {
            object oldOrigins;
            if (LoadJson(manifestPath).TryGetValue("allowed_origins", out oldOrigins)) foreach (object origin in (System.Collections.IEnumerable)oldOrigins) {
                string value = origin as string;
                if (value != null && System.Text.RegularExpressions.Regex.IsMatch(value, "^chrome-extension://[a-p]{32}/$")) origins.Add(value);
            }
        } catch { }
        var manifest = new { name = HostName, description = "PIP Peek window pinning", path = target, type = "stdio", allowed_origins = origins.Distinct().ToArray() };
        File.WriteAllText(manifestPath, Json.Serialize(manifest), new UTF8Encoding(false));
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Google\Chrome\NativeMessagingHosts\" + HostName)) key.SetValue("", manifestPath);
    }
    static byte[] Read(Stream input, int count) {
        byte[] data = new byte[count]; int offset = 0;
        while (offset < count) { int n = input.Read(data, offset, count-offset); if (n == 0) throw new EndOfStreamException(); offset += n; }
        return data;
    }
    static object Execute(string[] args, Dictionary<string,object> request) {
        object action;
        if (!request.TryGetValue("action", out action)) throw new Exception("Missing action.");
        if ((string)action == "probe") return new { ok = true, version = 1 };
        long value = 0;
        foreach (string arg in args) if (arg.StartsWith("--parent-window=")) long.TryParse(arg.Substring(16), out value);
        IntPtr hwnd = new IntPtr(value);
        if (value == 0 || !IsWindow(hwnd)) throw new Exception("The player window is unavailable.");
        hwnd = GetAncestor(hwnd, 2);
        uint pid; GetWindowThreadProcessId(hwnd, out pid);
        string process = Process.GetProcessById((int)pid).ProcessName;
        var title = new StringBuilder(1024); GetWindowText(hwnd, title, title.Capacity);
        if (!(process.Equals("chrome", StringComparison.OrdinalIgnoreCase) || process.Equals("chromium", StringComparison.OrdinalIgnoreCase)) || !title.ToString().StartsWith("PIP Peek") || !title.ToString().Contains("Player"))
            throw new Exception("Only the PIP Peek player can be pinned.");
        if ((string)action == "set") {
            object pinned;
            if (!request.TryGetValue("pinned", out pinned) || !(pinned is bool)) throw new Exception("Invalid pin state.");
            // NOMOVE | NOSIZE | NOACTIVATE: pin without resizing or stealing focus.
            if (!SetWindowPos(hwnd, new IntPtr((bool)pinned ? -1 : -2), 0, 0, 0, 0, 0x13)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        } else if ((string)action != "get") throw new Exception("Unsupported action.");
        return new { ok = true, pinned = (GetWindowLong(hwnd, -20) & 8) != 0 };
    }
    [STAThread]
    static void Main(string[] args) {
        if (!args.Any(arg => arg.StartsWith("chrome-extension://", StringComparison.Ordinal))) {
            bool quiet = args.Contains("--quiet");
            try {
                if (args.Contains("--uninstall")) Registry.CurrentUser.DeleteSubKey(@"Software\Google\Chrome\NativeMessagingHosts\" + HostName, false);
                else Install(args);
                if (!quiet) { Application.EnableVisualStyles(); MessageBox.Show(args.Contains("--uninstall") ? "PIP Peek On Top has been removed." : "PIP Peek On Top is ready.\n\nReturn to the player and press its pin button. No browser restart is needed.", "PIP Peek On Top", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            } catch (Exception error) {
                if (!quiet) MessageBox.Show("Setup could not finish:\n\n" + error.Message, "PIP Peek On Top", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
            return;
        }
        var input = Console.OpenStandardInput(); var output = Console.OpenStandardOutput();
        object response;
        try {
            int length = BitConverter.ToInt32(Read(input, 4), 0);
            if (length <= 0 || length > 4096) throw new Exception("Invalid message length.");
            response = Execute(args, Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(Read(input, length))));
        } catch (Exception error) { response = new { ok = false, error = error.Message }; }
        byte[] data = Encoding.UTF8.GetBytes(Json.Serialize(response));
        output.Write(BitConverter.GetBytes(data.Length), 0, 4); output.Write(data, 0, data.Length); output.Flush();
    }
}
