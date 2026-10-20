using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

static class Program
{
    [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); }
}

class Dll { public string Path { get; set; } = ""; public bool Enabled { get; set; } = true; }

class MainForm : Form
{
    readonly WebView2 wv = new() { Dock = DockStyle.Fill };
    List<Dll> dlls = new();
    readonly string store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BedrockLauncher", "dlls.json");

    public MainForm()
    {
        Text = "Bedrock Launcher"; Width = 1150; Height = 740; BackColor = Color.FromArgb(30, 30, 31);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(wv);
        try { dlls = JsonSerializer.Deserialize<List<Dll>>(File.ReadAllText(store)) ?? new(); } catch { }
        Load += async (_, _) => await Init();
    }

    async Task Init()
    {
        await wv.EnsureCoreWebView2Async();
        var c = wv.CoreWebView2;
        c.Settings.AreDevToolsEnabled = false;
        c.Settings.AreDefaultContextMenusEnabled = false;
        c.SetVirtualHostNameToFolderMapping("app.local", Path.Combine(AppContext.BaseDirectory, "web"),
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Deny);
        c.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://app.local/")) e.Cancel = true; };
        c.NewWindowRequested += (_, e) => e.Handled = true;
        c.WebMessageReceived += (_, e) => OnMessage(e.WebMessageAsJson);
        c.Navigate("https://app.local/index.html");
    }

    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(store)!);
        File.WriteAllText(store, JsonSerializer.Serialize(dlls));
    }

    void Post(object o) => Invoke(() => wv.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(o)));
    void PostDlls() => Post(new { type = "dlls", list = dlls.Select(d => new { name = Path.GetFileName(d.Path), enabled = d.Enabled }) });
    void Status(string t, bool done = false) => Post(new { type = "status", text = t, done });

    void OnMessage(string json)
    {
        // Only a fixed set of commands is accepted. DLL paths come from the file dialog, never from the page.
        using var doc = JsonDocument.Parse(json);
        string type = doc.RootElement.GetProperty("type").GetString();
        int i = doc.RootElement.TryGetProperty("i", out var p) ? p.GetInt32() : -1;
        switch (type)
        {
            case "ready": PostDlls(); break;
            case "add":
                using (var dlg = new OpenFileDialog { Filter = "DLL files|*.dll", Title = "Choose a client DLL" })
                    if (dlg.ShowDialog(this) == DialogResult.OK) { dlls.Add(new Dll { Path = dlg.FileName }); Save(); }
                PostDlls(); break;
            case "toggle": if (i >= 0 && i < dlls.Count) { dlls[i].Enabled = !dlls[i].Enabled; Save(); } PostDlls(); break;
            case "remove": if (i >= 0 && i < dlls.Count) { dlls.RemoveAt(i); Save(); } PostDlls(); break;
            case "launch": var list = dlls.Where(d => d.Enabled).Select(d => d.Path).ToList(); Task.Run(() => Launch(list)); break;
        }
    }

    void Launch(List<string> files)
    {
        try
        {
            foreach (var f in files) if (!File.Exists(f)) { Status("Missing file: " + f, true); return; }
            if (Process.GetProcessesByName("Minecraft.Windows").Length == 0)
            {
                Status("Starting Minecraft...");
                Process.Start(new ProcessStartInfo("minecraft://") { UseShellExecute = true });
            }
            Process game = null;
            for (int n = 0; n < 120 && game == null; n++)
            {
                var l = Process.GetProcessesByName("Minecraft.Windows");
                if (l.Length > 0) game = l[0]; else Thread.Sleep(500);
            }
            if (game == null) { Status("Minecraft did not start. Is Bedrock installed?", true); return; }
            if (files.Count == 0) { Status("Minecraft is running.", true); return; }
            Status("Waiting for the game to load...");
            Thread.Sleep(5000);
            foreach (var f in files)
            {
                Status("Injecting " + Path.GetFileName(f) + "...");
                string err = Injector.Inject(f, game.Id);
                if (err != null) { Status(err, true); return; }
            }
            Status("Done. Injected " + files.Count + " DLL(s).", true);
        }
        catch (Exception ex) { Status("Error: " + ex.Message, true); }
    }
}

static class Injector
{
    [DllImport("kernel32", SetLastError = true)] static extern IntPtr OpenProcess(uint a, bool i, int pid);
    [DllImport("kernel32", SetLastError = true)] static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr addr, uint size, uint type, uint prot);
    [DllImport("kernel32", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, uint size, out UIntPtr w);
    [DllImport("kernel32", SetLastError = true)] static extern IntPtr CreateRemoteThread(IntPtr h, IntPtr attr, uint stack, IntPtr start, IntPtr param, uint flags, out uint id);
    [DllImport("kernel32", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr mod, string name);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);

    public static string Inject(string dll, int pid)
    {
        dll = Path.GetFullPath(dll);
        var fi = new FileInfo(dll);
        var sec = fi.GetAccessControl();   // UWP apps need ALL APPLICATION PACKAGES access
        sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-15-2-1"), FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        fi.SetAccessControl(sec);

        IntPtr proc = OpenProcess(0x1F0FFF, false, pid);
        if (proc == IntPtr.Zero) return "Could not open the game process.";
        byte[] path = Encoding.Unicode.GetBytes(dll + "\0");
        IntPtr mem = VirtualAllocEx(proc, IntPtr.Zero, (uint)path.Length, 0x3000, 0x04);
        WriteProcessMemory(proc, mem, path, (uint)path.Length, out _);
        IntPtr load = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
        IntPtr th = CreateRemoteThread(proc, IntPtr.Zero, 0, load, mem, 0, out _);
        if (th == IntPtr.Zero) { CloseHandle(proc); return "Injection failed for " + Path.GetFileName(dll); }
        WaitForSingleObject(th, 10000);
        CloseHandle(th); CloseHandle(proc);
        return null;
    }
}
