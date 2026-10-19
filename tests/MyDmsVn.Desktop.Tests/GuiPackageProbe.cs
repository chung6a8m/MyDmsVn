// Compiled only into an owned temporary net48/x86 UIA process; never shipped.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

internal static class GuiPackageProbe
{
    private static string _stage = "startup";
    private static string _detail = "none";
    private static string _theme = "Light";
    private static Process _application;
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [DllImport("kernel32.dll")] private static extern int WerSetFlags(uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("shcore.dll")] private static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scale);

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        SetErrorMode(0x0001 | 0x0002);
        WerSetFlags(0x0002);
        Console.WriteLine("BITNESS=" + (IntPtr.Size * 8));
        try
        {
            if ((args.Length != 2 && args.Length != 3) || !Path.IsPathRooted(args[0])) throw new ArgumentException();
            _theme = args.Length == 3 ? args[2] : "Light";
            if (_theme != "Light" && _theme != "Dark") throw new ArgumentException();
            StartApplication(args[0]);
            if (args[1] == "startup-unavailable")
            {
                _stage = "owned-startup-dialog";
                var window = Window("MyDmsVn");
                var message = Find(window, "Startup.DatabaseUnavailable", ControlType.Text, contains: true);
                var text = message.Current.Name;
                if (text.Contains("127.0.0.1") || text.Contains("SqlException") || text.Contains("Password="))
                    throw new InvalidOperationException("Unsafe startup error.");
                Invoke(Find(window, "OK", ControlType.Button, contains: false));
                if (!_application.WaitForExit(5000) || _application.ExitCode != 2)
                    throw new InvalidOperationException("Startup application did not exit safely.");
                Console.WriteLine("STARTUP_SAFE_ERROR=PASS");
            }
            else if (args[1] == "catalog-audit")
            {
                var shell = Login();
                Catalog(shell, "Products", "GUI-PROD", "UIA Product", new[] { "Unit" }, new[] { "pcs" });
                Catalog(shell, "Warehouses", "GUI-WH", "UIA Warehouse", new[] { "Address" }, new[] { "GUI address" });
                Catalog(shell, "Employees", "GUI-EMP", "UIA Employee", new[] { "Phone" }, new[] { "0900123456" });
                Catalog(shell, "Customers", "GUI-CUST", "UIA Customer", new[] { "Address", "Phone", "Tax code" }, new[] { "GUI address", "0900123456", "P5-TAX" });
                CloseApplication(shell);
                Console.WriteLine("CATALOG_PRODUCT=PASS");
                Console.WriteLine("CATALOGS=PASS");
            }
            else if (args[1] == "login-audit")
            {
                _stage = "real-login";
                var shell = Login();
                Dump(shell);
                _stage = "logout";
                Invoke(Find(shell, "Sign out", ControlType.Button, false));
                _stage = "relogin";
                shell = Login();
                CloseApplication(shell);
                _application.Dispose();
                _stage = "restart";
                StartApplication(args[0]);
                shell = Login();
                CloseApplication(shell);
                Console.WriteLine("REAL_LOGIN=PASS");
                Console.WriteLine("LOGOUT_RELOGIN_RESTART=PASS");
            }
            else throw new ArgumentException("Unknown scenario.");
            if (!_application.HasExited) throw new InvalidOperationException("Application still running.");
            Console.WriteLine("APPLICATION_DRAINED=PASS");
            return 0;
        }
        catch (Exception error)
        {
            // Never print credential-bearing exceptions, element values or connection strings.
            Console.Error.WriteLine("GUI_PROBE_FAILED=" + _stage + ":" + error.GetType().Name + ":" + _detail);
            return 1;
        }
        finally
        {
            if (_application != null) _application.Dispose();
            // The outer suspended/job-owned supervisor drains any surviving descendants.
        }
    }

    private static void StartApplication(string executable)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = false,
        };
        var keys = new string[start.EnvironmentVariables.Count];
        start.EnvironmentVariables.Keys.CopyTo(keys, 0);
        foreach (var key in keys)
            if (key.StartsWith("MYDMSVN_GUI_P5_", StringComparison.OrdinalIgnoreCase)) start.EnvironmentVariables.Remove(key);
        _application = Process.Start(start);
    }

    private static void Catalog(AutomationElement shell, string title, string code, string name, string[] fields, string[] values)
    {
        _stage = "catalog-create-" + title;
        Invoke(Find(shell, title, ControlType.Button, false));
        var page = Find(shell, title, ControlType.TabItem, false);
        Console.WriteLine("PAGE_START=" + title);
        Dump(page);
        var results = Find(page, "Catalog results", ControlType.Table, false);
        SetInput(page, "Code", code, false);
        SetInput(page, "Name", name, false);
        for (var index = 0; index < fields.Length; index++) SetInput(page, fields[index], values[index], false);
        Invoke(Find(page, "Save", ControlType.Button, false));
        Named(results, code);
        Invoke(Find(page, "New", ControlType.Button, false));
        _stage = "catalog-select-row-" + title;
        Invoke(Find(page, "Open " + code, ControlType.Button, false));
        Wait(delegate { return InputValue(page, "Code") == code; }, "Selected row did not load editor.");
        _stage = "catalog-update-" + title;
        SetInput(page, "Name", name + " Updated", false);
        Invoke(Find(page, "Save", ControlType.Button, false));
        Named(results, name + " Updated");
        Invoke(Find(page, "Deactivate", ControlType.Button, false));
        Named(results, "Inactive");
        Invoke(Find(page, "Activate", ControlType.Button, false));
        Named(results, "Active");
        Console.WriteLine("CATALOG_FLOW=" + title + ":PASS");
    }

    private static AutomationElement Login()
    {
        var login = Window("MyDmsVn — Đăng nhập");
        Dump(login); // Names/patterns only, before any credential is entered.
        SetInput(login, "Username", "gui.p5.owner", false);
        SetInput(login, "Password", Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_OWNER_PASSWORD"), true);
        Invoke(Find(login, "Đăng nhập", ControlType.Button, false));
        var shell = Window("MyDmsVn");
        Find(shell, "GUI P5 Owner", ControlType.Text, false);
        var theme = Find(shell, "Light / Dark", ControlType.Button, false);
        if (theme.Current.HelpText != "Current theme: " + _theme) Invoke(theme);
        Wait(delegate { return theme.Current.HelpText == "Current theme: " + _theme; }, "Theme metadata did not match the requested mode.");
        Console.WriteLine("APPLICATION_THEME=" + _theme);
        return shell;
    }

    private static void CloseApplication(AutomationElement shell)
    {
        ((WindowPattern)shell.GetCurrentPattern(WindowPattern.Pattern)).Close();
        if (!_application.WaitForExit(5000) || _application.ExitCode != 0)
            throw new InvalidOperationException("Authenticated application did not close normally.");
    }

    private static void SetInput(AutomationElement root, string name, string value, bool password)
    {
        var editor = InputEditor(root, name);
        var candidates = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, name));
        if (editor.Current.IsPassword != password) throw new InvalidOperationException("Input accessibility semantics missing.");
        Wait(delegate { return editor.Current.IsEnabled; }, "Input disabled.");
        ((ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);
        if (password)
        {
            foreach (AutomationElement named in candidates)
            {
                object exposed;
                if (!named.Current.IsPassword && named.TryGetCurrentPattern(ValuePattern.Pattern, out exposed) &&
                    ((ValuePattern)exposed).Current.Value == value)
                    throw new InvalidOperationException("Masked wrapper exposes credential.");
            }
        }
        Console.WriteLine("INPUT_PATTERN=" + name + ":Value");
    }

    private static AutomationElement InputEditor(AutomationElement root, string name)
    {
        AutomationElement editor = null;
        var candidates = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, name));
        foreach (AutomationElement named in candidates)
        {
            var candidate = named.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (candidate == null && named.Current.ControlType == ControlType.Edit) candidate = named;
            if (candidate == null) continue;
            if (editor != null && !Automation.Compare(editor, candidate)) { _detail = "Ambiguous input selector"; throw new InvalidOperationException(); }
            editor = candidate;
        }
        if (editor == null) { _detail = "Input unavailable"; throw new InvalidOperationException(); }
        return editor;
    }

    private static string InputValue(AutomationElement root, string name)
    {
        var editor = InputEditor(root, name);
        if (editor.Current.IsPassword) throw new InvalidOperationException("Protected value cannot be inspected.");
        return ((ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
    }

    private static AutomationElement Named(AutomationElement root, string name)
    {
        AutomationElement result = null;
        Wait(delegate
        {
            var candidates = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
            if (candidates.Count > 1) { _detail = "Ambiguous accessible result name"; return false; }
            if (candidates.Count == 1) result = candidates[0];
            return result != null;
        }, "Accessible name unavailable.");
        return result;
    }

    private static void Dump(AutomationElement root)
    {
        var children = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        for (var index = 0; index < children.Count && index < 80; index++)
        {
            var item = children[index];
            if (item.Current.IsPassword) continue;
            Console.WriteLine("UIA=" + item.Current.ControlType.ProgrammaticName + ":" + item.Current.Name);
        }
    }

    private static AutomationElement Window(string title)
    {
        AutomationElement result = null;
        Wait(delegate
        {
            var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, _application.Id));
            foreach (AutomationElement candidate in windows)
                if (candidate.Current.Name == title) { result = candidate; return true; }
            return false;
        }, "Owned window unavailable.");
        var handle = new IntPtr(result.Current.NativeWindowHandle);
        int scale;
        if (GetScaleFactorForMonitor(MonitorFromWindow(handle, 2), out scale) != 0) throw new InvalidOperationException("Monitor scale unavailable.");
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_EXPECTED_SCALE") ?? "100";
        if ((expected != "100" && expected != "125") || scale.ToString() != expected) throw new InvalidOperationException("Desktop scale differs from declared configuration.");
        Console.WriteLine("MONITOR_SCALE=" + scale + ";WINDOW_DPI=" + GetDpiForWindow(handle));
        return result;
    }

    private static AutomationElement Find(AutomationElement root, string name, ControlType type, bool contains)
    {
        AutomationElement result = null;
        Wait(delegate
        {
            var candidates = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, type));
            foreach (AutomationElement candidate in candidates)
            {
                var actual = candidate.Current.Name;
                if (contains ? actual.Contains(name) : actual == name)
                {
                    if (result != null) { _detail = "Ambiguous accessible selector"; throw new InvalidOperationException(); }
                    result = candidate;
                }
            }
            return result != null;
        }, "Required accessible element unavailable.");
        return result;
    }

    private static void Invoke(AutomationElement element)
    {
        Wait(delegate { return element.Current.IsEnabled; }, "Action stayed disabled.");
        ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static void Wait(Func<bool> condition, string failure)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(15))
        {
            try { if (condition()) return; }
            catch (ElementNotAvailableException) { }
            Thread.Sleep(25); // Finite predicate polling; never a guessed UI delay.
        }
        _detail = failure;
        throw new TimeoutException();
    }
}
