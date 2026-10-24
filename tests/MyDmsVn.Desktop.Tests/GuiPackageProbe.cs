// Compiled only into an owned temporary net48/x86 UIA process; never shipped.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

internal static class GuiPackageProbe
{
    private static readonly System.Collections.Generic.Dictionary<string, int> CatalogIds = new System.Collections.Generic.Dictionary<string, int>();
    private static string _stage = "startup";
    private static string _detail = "none";
    private static string _theme = "Light";
    private static int _salesRow = 1;
    private static Process _application;
    private static IntPtr MainWindow;
    private static readonly Stopwatch TraceClock = Stopwatch.StartNew();
    private sealed class CachedInput { internal AutomationElement Named; internal AutomationElement Editor; }
    private static readonly System.Collections.Generic.Dictionary<string, CachedInput> InputEditors = new System.Collections.Generic.Dictionary<string, CachedInput>();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [DllImport("kernel32.dll")] private static extern int WerSetFlags(uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr key, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    private delegate bool EnumerateWindow(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr window, EnumerateWindow callback, IntPtr state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int length);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendMessageSelection(IntPtr window, uint message, out int start, out int end);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr length, System.Text.StringBuilder text, uint flags, uint timeout, out UIntPtr result);
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(IntPtr window, uint objectId, ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible accessible);

    private static string NativeDescription(AutomationElement element)
    {
        var window = new IntPtr(element.Current.NativeWindowHandle);
        uint owner;
        GetWindowThreadProcessId(window, out owner);
        if (window == IntPtr.Zero || owner != _application.Id) throw new InvalidOperationException("Accessible description HWND ownership mismatch.");
        var id = typeof(Accessibility.IAccessible).GUID;
        Accessibility.IAccessible accessible;
        // Public MSAA client contract: https://learn.microsoft.com/windows/win32/api/oleacc/nf-oleacc-accessibleobjectfromwindow
        Marshal.ThrowExceptionForHR(AccessibleObjectFromWindow(window, 0xfffffffc, ref id, out accessible)); // OBJID_CLIENT
        try { return accessible.get_accDescription(0) ?? ""; }
        finally { if (Marshal.IsComObject(accessible)) Marshal.ReleaseComObject(accessible); }
    }

    private static string NativeDefaultAction(AutomationElement element)
    {
        var window = new IntPtr(element.Current.NativeWindowHandle);
        uint owner;
        GetWindowThreadProcessId(window, out owner);
        if (window == IntPtr.Zero || owner != _application.Id) throw new InvalidOperationException("Accessible action HWND ownership mismatch.");
        var id = typeof(Accessibility.IAccessible).GUID;
        Accessibility.IAccessible accessible;
        Marshal.ThrowExceptionForHR(AccessibleObjectFromWindow(window, 0xfffffffc, ref id, out accessible));
        try { return accessible.get_accDefaultAction(0) ?? ""; }
        finally { if (Marshal.IsComObject(accessible)) Marshal.ReleaseComObject(accessible); }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        SetErrorMode(0x0001 | 0x0002);
        WerSetFlags(0x0002);
        Console.WriteLine("BITNESS=" + (IntPtr.Size * 8));
        Console.WriteLine("CULTURE=" + System.Globalization.CultureInfo.CurrentCulture.Name);
        try
        {
            // Configure only the UIA client; the launched product keeps its shipped DPI settings.
            if (!SetProcessDpiAwarenessContext(new IntPtr(-4)) || GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) != 2)
                throw new InvalidOperationException("UIA per-monitor DPI awareness unavailable.");
            Console.WriteLine("UIA_DPI_CONTEXT=PerMonitorV2");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING")) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING")))
                throw new InvalidOperationException("Administrative SQL environment reached the UI probe.");
            Console.WriteLine("ADMIN_SQL_ENV_EXCLUDED=PASS");
            if ((args.Length != 2 && args.Length != 3) || !Path.IsPathRooted(args[0])) throw new ArgumentException();
            _theme = args.Length == 3 ? args[2] : "Light";
            if (_theme != "Light" && _theme != "Dark") throw new ArgumentException();
            if (args[1] == "keyboard-focus-audit" || args[1] == "keyboard-focus-refusal-audit")
            {
                AuditOwnedKeyboardForeground(args[0], args[1] == "keyboard-focus-refusal-audit");
                return 0;
            }
            StartApplication(args[0]);
            if (args[1].StartsWith("startup-schema-", StringComparison.Ordinal))
            {
                var expected = args[1].Substring("startup-schema-".Length);
                var window = Window("MyDmsVn");
                Find(window, expected, ControlType.Text, true);
                Invoke(Find(window, "OK", ControlType.Button, false));
                if (!_application.WaitForExit(5000) || _application.ExitCode != 2)
                    throw new InvalidOperationException("Schema-refused application did not exit safely.");
                Console.WriteLine("PACKAGED_SCHEMA_REFUSED=" + expected + ":PASS");
            }
            else if (args[1] == "startup-unavailable")
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
            else if (args[1] == "uin-t13-attended")
            {
                _stage = "uin-t13-attended-setup";
                var shell = Login();
                Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
                var page = Find(shell, "Goods Receipts", ControlType.TabItem, false);
                Invoke(Find(page, "New", ControlType.Button, false));
                Find(page, "Note", ControlType.Edit, false);
                var barrier = Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_BARRIER");
                File.WriteAllText(Path.Combine(barrier, "ready"), "Goods draft ready for human observation.");
                Console.WriteLine("UIN_T13_ATTENDED_READY=PASS");
                _stage = "uin-t13-attended-human";
                Wait(delegate { return File.Exists(Path.Combine(barrier, "complete")); },
                    "UI Next T13 attended walkthrough was not completed within its owned budget.", 1200);
                CloseApplication(shell);
                Console.WriteLine("UIN_T13_ATTENDED_DRAINED=PASS");
            }
            else if (args[1] == "bcf-lookup-attended")
            {
                _stage = "bcf-attended-setup";
                var shell = Login();
                Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
                var page = Find(shell, "Goods Receipts", ControlType.TabItem, false);
                var barrier = Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_BARRIER");
                var scope = Environment.GetEnvironmentVariable("MYDMSVN_BCF_ATTENDED_SCOPE") ?? "both";
                if (scope != "both" && scope != "detail") throw new ArgumentException("Unknown attended scope.");
                Console.WriteLine("ATTENDED_LOOKUP_SCOPE=" + scope);
                if (scope == "both")
                    ObserveAttendedPhase(shell, page, barrier, "list", new[] { "Filter by warehouse" }, "enter-detail");
                // New admits query-only navigation and cancels the filter presentation.
                // After a human returns to chat, do not attempt native setup keys:
                // foreground admission remains strict. The human selects the initial
                // committed headers before testing them in the next phase.
                Invoke(Find(page, "New", ControlType.Button, false));
                Find(page, "Note", ControlType.Edit, false);
                var toggle = Find(shell, "Theme toggle", ControlType.Button, false);
                if (NativeDescription(toggle) != "Current theme: Light") Invoke(toggle);
                Wait(delegate { return NativeDescription(toggle) == "Current theme: Light"; }, "Detail setup theme unavailable.");
                ObserveAttendedPhase(shell, page, barrier, "detail", new[] { "Warehouse", "Employee" }, "complete");
                CloseApplication(shell);
                Console.WriteLine("ATTENDED_LOOKUP_SESSION_DRAINED=PASS");
            }
            else if (args[1] == "invalid-audit")
            {
                var shell = Login();
                _stage = "receipt-invalid-fields";
                Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
                var page = Find(shell, "Goods Receipts", ControlType.TabItem, false);
                Invoke(Find(page, "New", ControlType.Button, false));
                Invoke(Find(page, "Add line", ControlType.Button, false));
                SetInput(page, "Quantity", 0m.ToString("F4"), false);
                SetInput(page, "Note", "GUI invalid input retained", false);
                Invoke(Find(page, "Save", ControlType.Button, false));
                Find(page, "Quantity must be greater than zero", ControlType.Text, true);
                if (InputValue(page, "Note") != "GUI invalid input retained") throw new InvalidOperationException("Invalid draft input was lost.");
                CloseApplication(shell);
                Console.WriteLine("INVALID_FIELDS_NO_WRITE=PASS");
            }
            else if (args[1] == "negative-audit")
            {
                if (_theme == "Dark")
                {
                    var ownerShell = Login();
                    Invoke(Find(ownerShell, "Sign out", ControlType.Button, false));
                }
                _stage = "disabled-login";
                var login = Window("MyDmsVn — Đăng nhập");
                SetInput(login, "Username", "gui.p5.disabled", false);
                SetInput(login, "Password", Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_ACTOR_PASSWORD"), true);
                Invoke(Find(login, "Đăng nhập", ControlType.Button, false));
                Find(login, "Tên đăng nhập hoặc mật khẩu không đúng.", ControlType.Pane, true);
                Console.WriteLine("DISABLED_LOGIN_REJECTED=PASS");
                var shell = Login("gui.p5.deny", Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_ACTOR_PASSWORD"), "GUI P5 Deny");
                Invoke(Find(shell, "Products", ControlType.Button, false));
                var page = Find(shell, "Products", ControlType.TabItem, false);
                SetInput(page, "Code", "GUI-DENY", false);
                SetInput(page, "Name", "Must not persist", false);
                SetInput(page, "Unit", "pcs", false);
                var save = Find(page, "Save", ControlType.Button, false);
                Wait(delegate { return save.Current.IsEnabled; }, "Writer grant did not enable Save before revocation.");
                var barrier = Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_BARRIER");
                File.WriteAllText(Path.Combine(barrier, "ready"), "ready");
                Wait(delegate { return File.Exists(Path.Combine(barrier, "release")); }, "Permission update was not released.");
                _stage = "server-direct-deny";
                Invoke(save); // Existing UI still grants Write; the actual server must reauthorize this request.
                // The catalog inline error and shell notification both expose this
                // text. Require the owned page's error without weakening uniqueness.
                Find(page, "The operation is not permitted.", ControlType.Text, true);
                if (InputValue(page, "Code") != "GUI-DENY") throw new InvalidOperationException("Denied edit was lost.");
                CloseApplication(shell);
                Console.WriteLine("SERVER_DIRECT_DENY=PASS");
            }
            else if (args[1].StartsWith("sales-", StringComparison.Ordinal))
            {
                var shell = Login("gui.sal.writer", Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_ACTOR_PASSWORD"), "SAL Writer");
                Console.WriteLine("SALES_ACTUAL_LOGIN=PASS");
                Sales(shell, args[0], args[1]);
                CloseApplication(shell);
            }
            else if (args[1] == "inventory-deny")
            {
                var shell = Login("gui.bci.receipt.writer", Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_ACTOR_PASSWORD"), "BCI Receipt Writer");
                Console.WriteLine("ORDINARY_RECEIPT_ACTOR_LOGIN=PASS");
                Receipt(shell, args[0], args[1]);
                CloseApplication(shell);
            }
            else if (args[1] == "inventory-audit" || args[1] == "inventory-stale" || args[1] == "inventory-fault" || args[1] == "inventory-lookup")
            {
                var shell = Login();
                Catalog(shell, "Products", "GUI-PROD", "UIA Product", new[] { "Unit" }, new[] { "pcs" });
                Catalog(shell, "Warehouses", "GUI-WH", "UIA Warehouse", new[] { "Address" }, new[] { "GUI address" });
                Catalog(shell, "Employees", "GUI-EMP", "UIA Employee", new[] { "Phone" }, new[] { "0900123456" });
                Receipt(shell, args[0], args[1]);
                if (args[1] != "inventory-lookup") CloseApplication(shell);
                if (args[1] == "inventory-fault")
                {
                    _application.Dispose();
                    StartApplication(args[0]);
                    CloseApplication(Login());
                    Console.WriteLine("FAULT_REMOVED_STARTUP_REVALIDATED=PASS");
                }
                if (args[1] == "inventory-audit" || args[1] == "inventory-fault")
                {
                    Console.WriteLine("INLINE_RECEIPT_NATIVE_EDIT=PASS");
                    Console.WriteLine("RECEIPT_POST_STOCK=PASS");
                }
            }
            else if (args[1] == "catalog-audit")
            {
                var shell = Login();
                Catalog(shell, "Products", "GUI-PROD", "UIA Product", new[] { "Unit" }, new[] { "pcs" });
                ProductDuplicateCode(shell);
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
            else if (args[1] == "dateinput-audit")
            {
                var shell = Login();
                AuditPackagedDateInputs(shell);
                CloseApplication(shell);
                Console.WriteLine("DATEINPUT_ACTUAL_LOGIN=PASS");
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
            // Method metadata only: never exception messages, arguments, values or SQL identities.
            var frames = new StackTrace(error, false).GetFrames();
            if (frames != null)
                for (var index = 0; index < frames.Length && index < 4; index++)
                {
                    var method = frames[index].GetMethod();
                    Console.Error.WriteLine("GUI_PROBE_FAILURE_SITE=" + method.DeclaringType.FullName + "." + method.Name);
                }
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
        InputEditors.Clear();
        MainWindow = IntPtr.Zero;
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

    private static void AuditOwnedKeyboardForeground(string executable, bool refusing)
    {
        _stage = "owned-keyboard-foreground";
        var signal = "Local\\MyDmsVn.GuiFocus." + Guid.NewGuid().ToString("N");
        using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset, signal))
        {
            _application = Process.Start(new ProcessStartInfo(executable, "focus-target " + signal + (refusing ? " refuse" : ""))
            { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable) });
            if (!ready.WaitOne(TimeSpan.FromSeconds(5))) throw new TimeoutException("Owned distraction unavailable.");
            var target = Window("GUI focus target");
            Find(target, "Focus target editor", ControlType.ComboBox, false);
            var handle = new IntPtr(target.Current.NativeWindowHandle);
            if (refusing)
            {
                var rejected = false;
                try { SetInput(target, "Focus target editor", "must not be written", false); }
                catch (TimeoutException) { rejected = true; }
                if (!rejected || InputValue(target, "Focus target editor") != "")
                    throw new InvalidOperationException("Input must reject unavailable focus without changing the editor.");
                Console.WriteLine("REFUSED_LOOKUP_FOCUS_NO_INPUT=PASS");
            }
            else
            {
                if (GetForegroundWindow() == handle) throw new InvalidOperationException("Target must start in the background.");
                Console.WriteLine("BACKGROUND_TARGET_OBSERVED=PASS");
                SendOwnedLookupKey(InputEditor(target, "Focus target editor"), 0x73);
                if (GetForegroundWindow() != handle) throw new InvalidOperationException("Keyboard target did not own foreground.");
                Find(target, "F4 delivered", ControlType.Text, false);
            }
            ((WindowPattern)target.GetCurrentPattern(WindowPattern.Pattern)).Close();
            if (!_application.WaitForExit(5000) || _application.ExitCode != 0) throw new InvalidOperationException("Synthetic focus target did not drain.");
            Console.WriteLine("OWNED_LOOKUP_FOREGROUND_RECOVERED=PASS");
        }
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
        Invoke(Find(page, title == "Products" ? "Add product" : "New", ControlType.Button, false));
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
        if (title != "Products")
        {
            var row = Named(results, "Result " + code);
            var id = row.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.HelpTextProperty, "ID"));
            if (id == null) throw new InvalidOperationException("Catalog ID column inaccessible.");
            CatalogIds[title] = int.Parse(id.Current.Name, System.Globalization.CultureInfo.InvariantCulture);
        }
        Console.WriteLine("CATALOG_FLOW=" + title + ":PASS");
    }

    private static void ProductDuplicateCode(AutomationElement shell)
    {
        _stage = "product-duplicate-code";
        var page = Find(shell, "Products", ControlType.TabItem, false);
        Invoke(Find(page, "Add product", ControlType.Button, false));
        SetInput(page, "Code", "GUI-PROD", false);
        SetInput(page, "Name", "Duplicate must remain a draft", false);
        SetInput(page, "Unit", "pcs", false);
        Invoke(Find(page, "Save", ControlType.Button, false));
        Find(page, "A product with this code already exists.", ControlType.Text, true);
        if (InputValue(page, "Code") != "GUI-PROD" ||
            InputValue(page, "Name") != "Duplicate must remain a draft")
            throw new InvalidOperationException("Duplicate Code discarded the typed draft.");
        Console.WriteLine("PRODUCT_DUPLICATE_CODE_INLINE=PASS");
    }

    private static AutomationElement Login()
    {
        return Login("gui.p5.owner", Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_OWNER_PASSWORD"), "GUI P5 Owner");
    }

    private static AutomationElement Login(string username, string password, string displayName)
    {
        var login = Window("MyDmsVn — Đăng nhập", 30);
        Dump(login); // Names/patterns only, before any credential is entered.
        SetInput(login, "Username", username, false);
        SetInput(login, "Password", password, true);
        Invoke(Find(login, "Đăng nhập", ControlType.Button, false));
        AutomationElement shell;
        try { shell = Window("MyDmsVn", 30); }
        catch
        {
            foreach (AutomationElement pane in login.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane)))
            {
                if (pane.Current.Name.StartsWith("Tên đăng nhập hoặc mật khẩu không đúng.", StringComparison.Ordinal)) Console.WriteLine("LOGIN_OUTCOME=INVALID_CREDENTIALS");
                if (pane.Current.Name.StartsWith("Không thể đăng nhập.", StringComparison.Ordinal)) Console.WriteLine("LOGIN_OUTCOME=SAFE_SERVICE_ERROR");
            }
            throw;
        }
        Find(shell, displayName, ControlType.Text, false);
        Dump(shell);
        var theme = Find(shell, "Theme toggle", ControlType.Button, false);
        // The stock icon button's UIA HelpText is its action tooltip. Current theme
        // is the native MSAA AccessibleDescription, not that tooltip or a guessed palette.
        var themeState = NativeDescription(theme);
        if (themeState != "Current theme: Light" && themeState != "Current theme: Dark")
            throw new InvalidOperationException("Unrecognized accessible theme state.");
        if (themeState != "Current theme: " + _theme) Invoke(theme);
        Wait(delegate { return NativeDescription(theme) == "Current theme: " + _theme; }, "Theme metadata did not match the requested mode.");
        Console.WriteLine("THEME_STATE_PROVIDER=MSAA.Description");
        Console.WriteLine("APPLICATION_THEME=" + _theme);
        return shell;
    }

    private static void AuditPackagedDateInputs(AutomationElement shell)
    {
        _stage = "dateinput-sales-filters";
        Invoke(Find(shell, "Sales Receipts", ControlType.Button, false));
        var sales = Find(shell, "Sales Receipts", ControlType.TabItem, false);
        AuditPackagedDateField(sales, "From date", "12/10/2025", 4,
            "12/11/2025", "12/12/2025", "");
        AuditPackagedDateField(sales, "To date", "05/01/2026", 4,
            "05/02/2026", "05/03/2026", "");
        Console.WriteLine("DATEINPUT_SALES_FILTERS_NATIVE=PASS");

        _stage = "dateinput-sales-required";
        Invoke(Find(sales, "New", ControlType.Button, false));
        AuditPackagedDateField(sales, "Sale date", "12/10/2026", 4,
            "12/11/2026", "12/12/2026", null);
        Console.WriteLine("DATEINPUT_SALES_REQUIRED_NATIVE=PASS");

        _stage = "dateinput-goods-required";
        Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
        var goods = Find(shell, "Goods Receipts", ControlType.TabItem, false);
        Invoke(Find(goods, "New", ControlType.Button, false));
        AuditPackagedDateField(goods, "Receipt date", "12/10/2026", 4,
            "12/11/2026", "12/12/2026", null);
        Console.WriteLine("DATEINPUT_GOODS_REQUIRED_NATIVE=PASS");
    }

    private static void AuditPackagedDateField(AutomationElement page, string name, string raw,
        int caret, string first, string second, string accepted)
    {
        var editor = InputEditor(page, name);
        var original = InputValue(page, name);
        if (accepted != null && original != accepted)
            throw new InvalidOperationException("Nullable date did not begin empty.");
        var trigger = DateTrigger(page, name);
        AssertDateGeometry(page, name, editor, trigger);
        SetInput(page, name, raw, false); // Setup only; subsequent steps use owned native keys.
        FocusOwnedEditor(editor, name);
        SetOwnedSelection(editor, caret);
        SendOwnedKey(editor, 0x26); // VK_UP
        AssertOwnedDateStep(editor, name, first, caret);
        SendOwnedKey(editor, 0x26);
        AssertOwnedDateStep(editor, name, second, caret);

        var pending = InputValue(page, name);
        if (NativeDefaultAction(trigger) != "Open calendar")
            throw new InvalidOperationException("Date trigger did not begin collapsed.");
        ClickOwnedDateTrigger(trigger);
        Wait(delegate { return NativeDefaultAction(trigger) == "Close calendar"; }, "Date popup did not open from the embedded trigger.");
        ClickOwnedDateTrigger(trigger);
        Wait(delegate { return NativeDefaultAction(trigger) == "Open calendar"; }, "Date popup did not close from the embedded trigger.");
        if (InputValue(page, name) != pending)
            throw new InvalidOperationException("Calendar dismissal changed pending date text.");

        FocusOwnedEditor(editor, name);
        SendOwnedKey(editor, 0x73); // VK_F4 opens the same popup from the native editor.
        Wait(delegate { return NativeDefaultAction(trigger) == "Close calendar"; }, "Date popup did not open from F4.");
        ClickOwnedDateTrigger(trigger);
        Wait(delegate { return NativeDefaultAction(trigger) == "Open calendar"; }, "F4 date popup did not cancel.");
        if (InputValue(page, name) != pending)
            throw new InvalidOperationException("F4 calendar dismissal changed pending date text.");

        FocusOwnedEditor(editor, name);
        SendOwnedKey(editor, 0x1B); // VK_ESCAPE cancels the pending text before leaving the page.
        Wait(delegate { return InputValue(page, name) == original; }, "Date cancel did not restore the accepted display.");
        Console.WriteLine("DATEINPUT_FIELD=" + name + ";NATIVE_STEPS=2;TRIGGER_CANCEL=PASS");
    }

    private static void AssertOwnedDateStep(AutomationElement editor, string name, string expected, int caret)
    {
        Wait(delegate { return ((ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern)).Current.Value == expected; },
            "Native date step did not produce the expected segment value.");
        int start, end;
        SendMessageSelection(new IntPtr(editor.Current.NativeWindowHandle), 0x00B0, out start, out end); // EM_GETSEL
        if (start != caret || end != caret || !editor.Current.HasKeyboardFocus)
            throw new InvalidOperationException("Native date step changed caret, selection or focus: " + name);
    }

    private static void SetOwnedSelection(AutomationElement editor, int caret)
    {
        var handle = new IntPtr(editor.Current.NativeWindowHandle);
        uint owner;
        GetWindowThreadProcessId(handle, out owner);
        if (handle == IntPtr.Zero || owner != _application.Id || !editor.Current.HasKeyboardFocus ||
            !PostMessage(handle, 0x00B1, new IntPtr(caret), new IntPtr(caret))) // EM_SETSEL
            throw new InvalidOperationException("Owned native date selection failed.");
        Wait(delegate
        {
            int start, end;
            SendMessageSelection(handle, 0x00B0, out start, out end);
            return start == caret && end == caret;
        }, "Native date caret could not be positioned.");
    }

    private static AutomationElement DateTrigger(AutomationElement page, string name)
    {
        var field = Named(page, name);
        var triggers = field.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Open calendar"));
        if (triggers.Count != 1) throw new InvalidOperationException("Date field must own one calendar trigger.");
        return triggers[0];
    }

    private static void ClickOwnedDateTrigger(AutomationElement trigger)
    {
        var handle = new IntPtr(trigger.Current.NativeWindowHandle);
        uint owner;
        GetWindowThreadProcessId(handle, out owner);
        if (handle == IntPtr.Zero || owner != _application.Id || !trigger.Current.IsEnabled)
            throw new InvalidOperationException("Date trigger is not an owned enabled HWND.");
        var rectangle = trigger.Current.BoundingRectangle;
        if (rectangle.Width < 24 || rectangle.Height < 24)
            throw new InvalidOperationException("Calendar trigger hit area is too small.");
        var client = new IntPtr(((int)(rectangle.Height / 2) << 16) | ((int)(rectangle.Width / 2) & 0xffff));
        if (!PostMessage(handle, 0x0201, new IntPtr(1), client) || !PostMessage(handle, 0x0202, IntPtr.Zero, client))
            throw new InvalidOperationException("Owned date trigger click failed.");
    }

    private static void AssertDateGeometry(AutomationElement page, string name,
        AutomationElement editor, AutomationElement trigger)
    {
        var outer = Named(page, name).Current.BoundingRectangle;
        var edit = editor.Current.BoundingRectangle;
        var action = trigger.Current.BoundingRectangle;
        var windowDpi = GetDpiForWindow(MainWindow);
        if (windowDpi == 0) throw new InvalidOperationException("Date window DPI is unavailable.");
        var scale = windowDpi / 96.0;
        if (outer.Height < 28 * scale || outer.Height > 60 * scale ||
            edit.Width < 70 * scale || edit.Right > action.Left ||
            action.Left < outer.Left || action.Right > outer.Right ||
            action.Top < outer.Top || action.Bottom > outer.Bottom)
            throw new InvalidOperationException("Date text or trigger geometry is clipped/overlapping.");
        Console.WriteLine("DATEINPUT_GEOMETRY=" + name + ";WINDOW_DPI=" + windowDpi +
            ";FIELD=" + (int)outer.Width + "x" + (int)outer.Height +
            ";TEXT_WIDTH=" + (int)edit.Width + ";TRIGGER=" + (int)action.Width + "x" + (int)action.Height);
    }

    private static void Sales(AutomationElement shell, string executable, string scenario)
    {
        _stage = scenario;
        Invoke(Find(shell, "Sales Receipts", ControlType.Button, false));
        var page = Find(shell, "Sales Receipts", ControlType.TabItem, false);
        if (scenario == "sales-list-input")
        {
            SetSalesPageSize(page, 1);
            Invoke(Find(page, "Apply filters", ControlType.Button, false));
            Console.WriteLine("SALES_NATIVE_PAGE_SIZE=PASS");
            return;
        }
        if (scenario == "sales-inspect")
        {
            SetInput(page, "Search sales receipts", "SAL golden", false);
            Invoke(Find(page, "Apply filters", ControlType.Button, false));
            Invoke(SalesOpenAction(page));
            AssertSalesPosted(page);
            Console.WriteLine("SALES_LATER_RECEIPT_COST_IMMUTABLE=PASS");
            return;
        }
        CreateSalesGolden(page);
        var action = SalesOpenAction(page).Current.Name;
        Invoke(Find(page, "New", ControlType.Button, false));
        Invoke(Find(page, "Back to sales", ControlType.Button, false));
        Invoke(Find(page, "Refresh", ControlType.Button, false));
        Invoke(Find(page, action, ControlType.Button, false));
        Wait(delegate { return InputValue(page, "Note") == "SAL golden"; }, "Saved Sales draft did not reload.");
        _salesRow = 1;
        AssertSalesCell(page, "Quantity", 3m);
        AssertSalesCell(page, "Unit price", 25m);
        _salesRow = 2;
        AssertSalesCell(page, "Quantity", 2m);
        AssertSalesCell(page, "Unit price", 40m);
        AssertSalesCell(page, "Line amount", 0m);
        SalesSummary(page, "Posted total cost", "Posted cost: Not posted");
        Console.WriteLine("SALES_DUPLICATE_GIFT_RELOAD=PASS");
        if (scenario == "sales-stale" || scenario == "sales-stale-post")
        {
            SalesStale(page, executable, action, scenario == "sales-stale-post");
            return;
        }
        if (scenario.StartsWith("sales-deny-", StringComparison.Ordinal)) { SalesDeny(page, scenario.Substring("sales-deny-".Length)); return; }
        if (scenario == "sales-fault") SalesUncertainPost(page);
        else
        {
            Invoke(Find(page, "Post", ControlType.Button, false));
            AssertSalesPosted(page);
        }
        Console.WriteLine("SALES_POSTED_IMMUTABLE=PASS");
        if (scenario == "sales-audit") SalesKeyset(page);
    }

    private static void CreateSalesGolden(AutomationElement page)
    {
        Invoke(Find(page, "New", ControlType.Button, false));
        SetLookup(page, "Customer", "SAL-C - Sales Customer");
        SetLookup(page, "Warehouse", "SAL-W - Sales Warehouse");
        SetLookup(page, "Employee", "SAL-E - Sales Employee");
        SetInput(page, "Note", "SAL golden", false);
        AddSalesLine(page, 1, 3m, 25m);
        AddSalesLine(page, 2, 2m, 40m);
        var gift = InlineCell(page, "Gift");
        ((ValuePattern)gift.GetCurrentPattern(ValuePattern.Pattern)).SetValue("true");
        AssertSalesCell(page, "Unit price", 40m);
        AssertSalesCell(page, "Line amount", 0m);
        ((ValuePattern)gift.GetCurrentPattern(ValuePattern.Pattern)).SetValue("false");
        AssertSalesCell(page, "Line amount", 80m);
        ((ValuePattern)gift.GetCurrentPattern(ValuePattern.Pattern)).SetValue("true");
        InputEditor(page, "Note").SetFocus();
        SalesSummary(page, "Posted total cost", "Posted cost: Not posted");
        Invoke(Find(page, "Save", ControlType.Button, false));
        _stage = "sales-save";
        try { Find(page, "Saved · Draft", ControlType.Text, false); }
        catch { Console.WriteLine("SALES_SAVE_STATUS=" + SalesLabelText(page, "Sales operation status")); Dump(page); throw; }
        Invoke(Find(page, "Back to sales", ControlType.Button, false));
        Invoke(Find(page, "Refresh", ControlType.Button, false));
    }

    private static void AddSalesLine(AutomationElement page, int row, decimal quantity, decimal price, string product = "SAL-P - Sales Product")
    {
        var previousStage = _stage;
        _stage = "sales-line-" + row + "-add";
        Invoke(Find(page, "Add line", ControlType.Button, false));
        _salesRow = row;
        _stage = "sales-line-" + row + "-product";
        SetLookup(page, "Product", product);
        _stage = "sales-line-" + row + "-quantity";
        SetInput(page, "Quantity", quantity.ToString("F4"), false);
        _stage = "sales-line-" + row + "-price";
        SetInput(page, "Unit price", price.ToString("F4"), false);
        _stage = "sales-line-" + row + "-commit";
        InputEditor(page, "Note").SetFocus();
        _stage = previousStage;
    }

    private static void AssertSalesCell(AutomationElement page, string field, decimal expected)
    {
        try
        {
            Wait(delegate
            {
                AutomationElement cell;
                if (!TrySalesCell(page, field, out cell)) return false;
                var value = (ValuePattern)cell.GetCurrentPattern(ValuePattern.Pattern);
                decimal actual;
                return decimal.TryParse(value.Current.Value, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture, out actual) && actual == expected;
            }, "Sales cell value differs: " + field);
        }
        catch
        {
            Console.WriteLine("SALES_CELL_FAILURE=" + field + ";ROW=" + _salesRow);
            Dump(page);
            throw;
        }
    }

    private static AutomationElement SalesOpenAction(AutomationElement page)
    {
        var table = Find(page, "Sales receipt results", ControlType.Table, false);
        AutomationElement result = null;
        Wait(delegate
        {
            result = null;
            var count = 0;
            foreach (AutomationElement item in table.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
                if (item.Current.Name.StartsWith("Open ", StringComparison.Ordinal))
                {
                    result = item; count++;
                }
            return result != null && count == 1;
        }, "Sales result row absent.");
        return result;
    }

    private static void AssertSalesPosted(AutomationElement page)
    {
        Find(page, "Saved · Posted", ControlType.Text, false);
        SalesSummary(page, "Posted total cost", "Posted cost: " + 75m.ToString("N4"));
        SalesSummary(page, "Posted gross profit", "Gross profit: " + 0m.ToString("N4"));
        foreach (var name in new[] { "Save", "Post", "Add line", "Remove line" })
            if (Find(page, name, ControlType.Button, false).Current.IsEnabled) throw new InvalidOperationException("Posted Sales mutation enabled.");
        _salesRow = 2;
        AutomationElement giftCell;
        try
        {
            giftCell = null;
            Wait(delegate { return TrySalesCell(page, "Gift", out giftCell); },
                "Posted Sales gift cell unavailable.");
        }
        catch { Console.WriteLine("SALES_POSTED_GIFT_FAILURE;ROW=" + _salesRow); Dump(page); throw; }
        var gift = (ValuePattern)giftCell.GetCurrentPattern(ValuePattern.Pattern);
        if (!gift.Current.IsReadOnly) throw new InvalidOperationException("Posted gift still editable.");
        AssertSalesCell(page, "Unit price", 40m);
    }

    private static string[] SalesActionNames(AutomationElement page)
    {
        var table = Find(page, "Sales receipt results", ControlType.Table, false);
        var names = new System.Collections.Generic.List<string>();
        foreach (AutomationElement item in table.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
            if (item.Current.Name.StartsWith("Open ", StringComparison.Ordinal)) names.Add(item.Current.Name);
        return names.ToArray();
    }

    private static void AssertSalesPage(AutomationElement page, string action, decimal total)
    {
        Wait(delegate { return SalesOpenAction(page).Current.Name == action; }, "Filtered Sales header/order differs.");
        ReadOnlyValue(Find(page, "Sales receipt results", ControlType.Table, false), total.ToString("N4"));
    }

    private static void SalesKeyset(AutomationElement page)
    {
        _stage = "sales-list-distinct-product-fixtures";
        Invoke(Find(page, "Back to sales", ControlType.Button, false));
        var golden = SalesOpenAction(page).Current.Name;
        var known = new System.Collections.Generic.List<string>(); known.Add(golden);
        var added = new string[3];
        for (var index = 0; index < 3; index++)
        {
            Invoke(Find(page, "New", ControlType.Button, false));
            SetLookup(page, "Customer", "SAL-C - Sales Customer");
            SetLookup(page, "Warehouse", "SAL-W - Sales Warehouse");
            SetLookup(page, "Employee", "SAL-E - Sales Employee");
            SetInput(page, "Note", index == 0 ? "SAL list pure" : index == 1 ? "SAL list mixed" : "SAL list other-only", false);
            if (index == 0) AddSalesLine(page, 1, 1m, 3m);
            else if (index == 1)
            {
                AddSalesLine(page, 1, 1m, 2m);
                AddSalesLine(page, 2, 2m, 7m, "SAL-X - Other Sales Product"); // Whole bill16; matching subtotal2.
            }
            else AddSalesLine(page, 1, 1m, 11m, "SAL-X - Other Sales Product"); // Newest header must be excluded.
            Invoke(Find(page, "Save", ControlType.Button, false));
            Find(page, "Saved · Draft", ControlType.Text, false);
            Invoke(Find(page, "Back to sales", ControlType.Button, false));
            Invoke(Find(page, "Refresh", ControlType.Button, false));
            Wait(delegate { return SalesActionNames(page).Length == known.Count + 1; }, "New Sales list fixture missing.");
            foreach (var name in SalesActionNames(page)) if (!known.Contains(name))
            {
                if (added[index] != null) throw new InvalidOperationException("Ambiguous new Sales fixture.");
                added[index] = name;
            }
            if (added[index] == null) throw new InvalidOperationException("New Sales fixture identity unavailable.");
            known.Add(added[index]);
        }
        SetSalesPageSize(page, 1);
        SetLookup(page, "Filter by product", "SAL-P - Sales Product");
        Invoke(Find(page, "Apply filters", ControlType.Button, false));
        _stage = "sales-list-filtered-exact-headers-totals";
        AssertSalesPage(page, added[1], 16m);
        var first = SalesOpenAction(page).Current.Name;
        Invoke(Find(page, "Next", ControlType.Button, false));
        AssertSalesPage(page, added[0], 3m);
        var second = SalesOpenAction(page).Current.Name;
        Invoke(Find(page, "Next", ControlType.Button, false));
        AssertSalesPage(page, golden, 75m);
        if (Find(page, "Next", ControlType.Button, false).Current.IsEnabled) throw new InvalidOperationException("Last filtered Sales page allows Next.");
        Console.WriteLine("SALES_FILTERED_ACTIONS=" + string.Join("|", new[] { first, second, golden }));
        Invoke(Find(page, "Previous", ControlType.Button, false));
        AssertSalesPage(page, added[0], 3m);
        Invoke(Find(page, "First", ControlType.Button, false));
        AssertSalesPage(page, added[1], 16m);
        if (Find(page, "Previous", ControlType.Button, false).Current.IsEnabled) throw new InvalidOperationException("First Sales page allows Previous.");
        Console.WriteLine("SALES_KEYSET_PRODUCT_TOTALS=PASS");
    }

    // Fixed semantic accessible names identify the labels. Read their actual native
    // HWND text with a finite send, rather than assuming the name equals rendered text.
    private static string SalesLabelText(AutomationElement page, string name)
    {
        var label = Find(page, name, ControlType.Text, false);
        var handle = new IntPtr(label.Current.NativeWindowHandle);
        uint process; GetWindowThreadProcessId(handle, out process);
        if (handle == IntPtr.Zero || process != _application.Id) throw new InvalidOperationException("Sales label HWND is not owned.");
        var text = new System.Text.StringBuilder(2048); UIntPtr count;
        if (SendMessageTimeout(handle, 0x000D, new UIntPtr(2048), text, 2, 2000, out count) == IntPtr.Zero)
            throw new TimeoutException("Owned Sales label text unavailable.");
        return text.ToString();
    }
    private static void SalesSummary(AutomationElement page, string name, string expected)
    {
        Wait(delegate { return SalesLabelText(page, name) == expected; }, "Sales summary differs: " + name);
        Console.WriteLine("SALES_SUMMARY=" + name + ":" + expected);
    }
    private static void SetSalesPageSize(AutomationElement page, int size)
    {
        // net48 NumericUpDown exposes only its two arrow buttons in this UIA tree.
        // Its named caption identifies one owned field, whose unique native EDIT is
        // the actual keyboard input. Never select an unrelated unnamed page EDIT.
        var caption = Find(page, "Page size", ControlType.Text, false);
        var field = GetParent(new IntPtr(caption.Current.NativeWindowHandle));
        uint process; GetWindowThreadProcessId(field, out process);
        if (field == IntPtr.Zero || process != _application.Id) throw new InvalidOperationException("Page size field is not owned.");
        var edits = new System.Collections.Generic.List<IntPtr>();
        EnumChildWindows(field, delegate(IntPtr window, IntPtr ignored)
        {
            var name = new System.Text.StringBuilder(256); GetClassName(window, name, name.Capacity);
            if (name.ToString().StartsWith("WindowsForms10.EDIT.", StringComparison.OrdinalIgnoreCase)) edits.Add(window);
            return true;
        }, IntPtr.Zero);
        if (edits.Count != 1) throw new InvalidOperationException("Owned Page size field must have exactly one native EDIT.");
        GetWindowThreadProcessId(edits[0], out process);
        if (process != _application.Id) throw new InvalidOperationException("Page size editor is not owned.");
        var editor = AutomationElement.FromHandle(edits[0]);
        FocusOwnedEditor(editor, "Page size");
        var value = (ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern);
        value.SetValue(size.ToString(System.Globalization.CultureInfo.CurrentCulture));
        InputEditor(page, "Search sales receipts").SetFocus(); // Admit NumericUpDown's normal validation.
        Wait(delegate { return value.Current.Value == size.ToString(); }, "Sales page size text did not commit.");
        Console.WriteLine("SALES_PAGE_SIZE_NATIVE_OWNERSHIP=PASS");
    }
    private static void SalesDeny(AutomationElement page, string permission)
    {
        Signal("sales-deny-ready"); AwaitSignal("sales-allow-denied-operation");
        if (permission == "Write") SetInput(page, "Note", "SAL denied input", false);
        Invoke(Find(page, permission == "Read" ? "Reload" : permission == "Write" ? "Save" : "Post", ControlType.Button, false));
        Wait(delegate { return SalesLabelText(page, "Sales operation status").Contains("not permitted"); }, "Sales direct Deny was not reported.");
        if (InputValue(page, "Note").Length != 0 || InputEditor(page, "Note").Current.IsEnabled)
            throw new InvalidOperationException("Denied Sales protected header was retained/editable.");
        if (LineGrid(page).FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox)).Count != 0)
            throw new InvalidOperationException("Denied Sales protected line rows remain exposed.");
        foreach (var name in new[] { "Save", "Post", "Add line", "Remove line" })
            if (Find(page, name, ControlType.Button, false).Current.IsEnabled) throw new InvalidOperationException("Denied Sales mutation enabled.");
        Console.WriteLine("SALES_" + permission.ToUpperInvariant() + "_DENIED_PROTECTED_STATE_CLEARED=PASS");
        Signal("sales-denied"); AwaitSignal("sales-denial-checked");
    }
    private static void SalesStale(AutomationElement page, string executable, string action, bool post)
    {
        var firstClient = _application;
        var firstWindow = MainWindow;
        StartApplication(executable);
        var otherClient = _application;
        var otherShell = Login("gui.sal.writer", Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_ACTOR_PASSWORD"), "SAL Writer");
        Invoke(Find(otherShell, "Sales Receipts", ControlType.Button, false));
        var otherPage = Find(otherShell, "Sales Receipts", ControlType.TabItem, false);
        _stage = "sales-stale-second-open";
        Invoke(Find(otherPage, action, ControlType.Button, false));
        // Match the existing receipt probe: Open loads asynchronously; List has no Note editor.
        var note = Find(otherPage, "Note", ControlType.Edit, false);
        if (note.Current.ProcessId != otherClient.Id) throw new InvalidOperationException("Second Sales editor is not owned.");
        Console.WriteLine("SALES_STALE_SECOND_NOTE_PRESENT=PASS;ENABLED=" + note.Current.IsEnabled);
        Wait(delegate { return InputValue(otherPage, "Note") == "SAL golden"; }, "Second actual client did not reload Sales Draft.");
        _stage = post ? "sales-stale-post" : "sales-stale-save";
        if (post)
        {
            Invoke(Find(otherPage, "Post", ControlType.Button, false));
            AssertSalesPosted(otherPage);
        }
        else
        {
            SetInput(otherPage, "Note", "SAL winner", false);
            Find(otherPage, "Unsaved changes", ControlType.Text, false);
            Invoke(Find(otherPage, "Save", ControlType.Button, false));
            Find(otherPage, "Saved · Draft", ControlType.Text, false);
        }
        Signal("sales-stale-winner-ready"); AwaitSignal("sales-allow-loser");
        _application = firstClient; MainWindow = firstWindow;
        if (!post) SetInput(page, "Note", "SAL losing raw input", false);
        Invoke(Find(page, post ? "Post" : "Save", ControlType.Button, false));
        Wait(delegate { return SalesLabelText(page, "Sales operation status").Contains(post ? "already posted" : "changed by another operation"); }, "Sales conflict not reported.");
        if (!post && InputValue(page, "Note") != "SAL losing raw input") throw new InvalidOperationException("Stale Sales input was lost.");
        if (post)
        {
            if (Find(page, "Post", ControlType.Button, false).Current.IsEnabled) throw new InvalidOperationException("Conflict permits automatic Post retry.");
            Invoke(Find(page, "Reload", ControlType.Button, false));
            AssertSalesPosted(page);
        }
        Signal("sales-stale-conflict-ready"); AwaitSignal("sales-stale-checked");
        _application = otherClient; CloseApplication(otherShell); otherClient.Dispose();
        _application = firstClient; MainWindow = firstWindow;
        Console.WriteLine(post ? "SALES_STALE_POST_ONE_EFFECT=PASS" : "SALES_STALE_SAVE_INPUT_RETAINED=PASS");
    }

    private static void SalesUncertainPost(AutomationElement page)
    {
        Signal("sales-post-ready"); AwaitSignal("sales-allow-post");
        Invoke(Find(page, "Post", ControlType.Button, false));
        Wait(delegate { return !Find(page, "Post", ControlType.Button, false).Current.IsEnabled; }, "Sales Post did not begin.");
        Wait(delegate { return Find(page, "Reload", ControlType.Button, false).Current.IsEnabled; }, "Sales timeout did not finish.", 55);
        foreach (var name in new[] { "New", "Save", "Post", "Add line", "Remove line" })
            if (Find(page, name, ControlType.Button, false).Current.IsEnabled) throw new InvalidOperationException("Uncertain Sales Post permits mutation.");
        if (InputEditor(page, "Note").Current.IsEnabled) throw new InvalidOperationException("Uncertain Sales header editable.");
        Signal("sales-uncertain"); AwaitSignal("sales-allow-reload");
        Invoke(Find(page, "Reload", ControlType.Button, false));
        Wait(delegate { return Find(page, "Post", ControlType.Button, false).Current.IsEnabled; }, "Explicit Sales reload did not recover Draft.");
        Find(page, "Saved · Draft", ControlType.Text, false);
        Signal("sales-reloaded"); AwaitSignal("sales-allow-retry");
        Invoke(Find(page, "Post", ControlType.Button, false)); // One explicit checked retry only.
        AssertSalesPosted(page);
        Console.WriteLine("SALES_UNCERTAIN_RELOAD_EXPLICIT_RETRY=PASS");
    }

    private static void Receipt(AutomationElement shell, string executable, string scenario)
    {
        _stage = "receipt-draft";
        Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
        var page = Find(shell, "Goods Receipts", ControlType.TabItem, false);
        Dump(page);
        Invoke(Find(page, "New", ControlType.Button, false));
        SetLookup(page, "Warehouse", "GUI-WH - UIA Warehouse Updated");
        SetLookup(page, "Employee", "GUI-EMP - UIA Employee Updated");
        SetInput(page, "Note", "GUI draft", false);
        Invoke(Find(page, "Add line", ControlType.Button, false));
        SetLookup(page, "Product", "GUI-PROD - UIA Product Updated");
        // Products intentionally has no primary ID column. Read the committed inline
        // selection's semantic key; independent SQL still checks the saved IDs/effects.
        var productHelp = InlineCell(page, "Product").Current.HelpText;
        var keyPrefix = "Committed key: ";
        if (!productHelp.StartsWith(keyPrefix, StringComparison.Ordinal)) throw new InvalidOperationException("Committed Product key unavailable.");
        CatalogIds["Products"] = int.Parse(productHelp.Substring(keyPrefix.Length).Split(';')[0], System.Globalization.CultureInfo.InvariantCulture);
        if (CatalogIds["Products"] <= 0) throw new InvalidOperationException("Committed Product key invalid.");
        Dump(page);
        SetInput(page, "Quantity", 2.5m.ToString("F4"), false);
        SetInput(page, "Unit cost", 4m.ToString("F4"), false);
        InputEditor(page, "Note").SetFocus();
        Find(page, 10m.ToString("F2"), ControlType.Text, false);
        Invoke(Find(page, "Save", ControlType.Button, false));
        Invoke(Find(page, "Back to receipts", ControlType.Button, false));
        var results = Find(page, "Goods receipt results", ControlType.Table, false);
        AutomationElement open = null;
        Wait(delegate
        {
            var buttons = results.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement button in buttons)
                if (button.Current.Name.StartsWith("Open ", StringComparison.Ordinal))
                {
                    if (open != null && !Automation.Compare(open, button)) throw new InvalidOperationException("Ambiguous receipt.");
                    open = button;
                }
            return open != null;
        }, "Saved receipt row unavailable.");
        var receiptAction = open.Current.Name;
        Invoke(Find(page, "New", ControlType.Button, false));
        Invoke(Find(page, "Back to receipts", ControlType.Button, false));
        Invoke(Find(results, receiptAction, ControlType.Button, false));
        Find(page, "Note", ControlType.Edit, false); // Open completes asynchronously; List has no editor.
        Wait(delegate { return InputValue(page, "Note") == "GUI draft"; }, "Saved draft did not reload.");
        _stage = "receipt-edit";
        SetInput(page, "Note", "GUI edited", false);
        SetInput(page, "Quantity", 3.125m.ToString("F4"), false);
        InputEditor(page, "Note").SetFocus();
        Find(page, 12.5m.ToString("F2"), ControlType.Text, false);
        if (scenario == "inventory-deny")
        {
            var committedProduct = InputValue(page, "Product");
            Signal("receipt-deny-ready");
            AwaitSignal("allow-denied-save");
            _stage = "receipt-server-direct-deny";
            Invoke(Find(page, "Save", ControlType.Button, false));
            Find(page, "The operation is not permitted.", ControlType.Pane, true);
            if (InputValue(page, "Note") != "GUI edited" || InputValue(page, "Product") != committedProduct ||
                decimal.Parse(InputValue(page, "Quantity"), System.Globalization.CultureInfo.CurrentCulture) != 3.125m ||
                decimal.Parse(InputValue(page, "Unit cost"), System.Globalization.CultureInfo.CurrentCulture) != 4m)
                throw new InvalidOperationException("Denied inline receipt input was lost.");
            if (Find(page, "Post", ControlType.Button, false).Current.IsEnabled)
                throw new InvalidOperationException("Denied dirty draft permits Post.");
            Console.WriteLine("INLINE_RECEIPT_DIRECT_DENY_INPUT_RETAINED=PASS");
            Signal("receipt-denied");
            AwaitSignal("allow-explicit-save");
            _stage = "receipt-explicit-save-after-regrant";
        }
        Invoke(Find(page, "Save", ControlType.Button, false));
        var post = Find(page, "Post", ControlType.Button, false);
        Wait(delegate { return post.Current.IsEnabled; }, "Saved draft cannot be posted.");
        if (scenario == "inventory-deny")
        {
            Console.WriteLine("INLINE_RECEIPT_EXPLICIT_SAVE_AFTER_REGRANT=PASS");
            Invoke(post);
            Find(page, "Posted", ControlType.Text, false);
            Wait(delegate { return !post.Current.IsEnabled; }, "Actor Posted receipt still permits Post.");
            Console.WriteLine("INLINE_RECEIPT_ACTOR_POST=PASS");
            return; // Existing inventory audit scenarios separately retain Balance/Card UI checks.
        }
        if (scenario == "inventory-lookup")
        {
            ChangedLookupsAndReopen(shell, page, executable, receiptAction);
            return;
        }
        if (scenario == "inventory-fault") PostAfterUncertainOutcome(page, post);
        else if (!PostWithSecondClient(page, post, executable, scenario, receiptAction)) return;
        _stage = "stock-balance";
        Invoke(Find(shell, "Tồn kho", ControlType.Button, false));
        var balance = Find(shell, "Tồn kho", ControlType.TabItem, false);
        SetInput(balance, "Search stock balances", "GUI-PROD", false);
        SetInput(balance, "Warehouse ID", CatalogIds["Warehouses"].ToString(), false);
        SetInput(balance, "Product ID", CatalogIds["Products"].ToString(), false);
        Invoke(Find(balance, "Refresh", ControlType.Button, false));
        Dump(balance);
        Find(balance, 3.125m.ToString("F4"), ControlType.Text, false);
        var balanceRows = Named(balance, "Stock balance results");
        ReadOnlyValue(balanceRows, "GUI-WH - UIA Warehouse Updated");
        ReadOnlyValue(balanceRows, "GUI-PROD - UIA Product Updated");
        ReadOnlyValue(balanceRows, 3.125m.ToString("F4"));
        Console.WriteLine("STOCK_BALANCE_TOTAL=PASS");
        _stage = "stock-card";
        Invoke(Find(shell, "Thẻ kho", ControlType.Button, false));
        var card = Find(shell, "Thẻ kho", ControlType.TabItem, false);
        SetInput(card, "Warehouse ID", CatalogIds["Warehouses"].ToString(), false);
        SetInput(card, "Product ID", CatalogIds["Products"].ToString(), false);
        Invoke(Find(card, "Refresh", ControlType.Button, false));
        Find(card, 3.125m.ToString("F4"), ControlType.Text, false);
        var cardRows = Named(card, "Stock card results");
        ReadOnlyValue(cardRows, receiptAction.Substring("Open ".Length));
        ReadOnlyValue(cardRows, "GoodsReceipt");
        ReadOnlyValue(cardRows, "GUI-WH - UIA Warehouse Updated");
        ReadOnlyValue(cardRows, "GUI-PROD - UIA Product Updated");
        ReadOnlyValue(cardRows, 3.125m.ToString("F4"));
        Console.WriteLine("STOCK_CARD_TOTAL=PASS");
    }

    private static void ChangedLookupsAndReopen(AutomationElement shell, AutomationElement page, string executable, string receiptAction)
    {
        var names = new[] { "Product", "Warehouse", "Employee" };
        var titles = new[] { "Products", "Warehouses", "Employees" };
        var codes = new[] { "GUI-PROD", "GUI-WH", "GUI-EMP" };
        var editors = new IntPtr[names.Length];
        for (var index = 0; index < names.Length; index++) editors[index] = new IntPtr(InputEditor(page, names[index]).Current.NativeWindowHandle);
        for (var index = 0; index < names.Length; index++)
        {
            var current = index;
            var updated = codes[index] + " - UIA " + names[index] + " Changed";
            var historical = codes[index] + " - UIA " + names[index] + " Updated";
            _stage = "same-process-lookup-" + names[index];
            Invoke(Find(shell, titles[index], ControlType.Button, false));
            var catalog = Find(shell, titles[index], ControlType.TabItem, false);
            var results = Find(catalog, "Catalog results", ControlType.Table, false);
            Invoke(Find(catalog, "Open " + codes[index], ControlType.Button, false));
            Wait(delegate { return InputValue(catalog, "Code") == codes[current]; }, "Changed catalog row did not load.");
            SetInput(catalog, "Name", "UIA " + names[index] + " Changed", false);
            Invoke(Find(catalog, "Save", ControlType.Button, false));
            Named(results, "UIA " + names[index] + " Changed");
            // Read the ORIGINAL offscreen receipt editor. No tab activation/Refresh can substitute for messaging.
            Wait(delegate { return OwnedEditorValue(editors[current]) == updated; }, "Offscreen lookup did not receive its same-process name change.");
            Console.WriteLine("SAME_PROCESS_LOOKUP_REFRESH=" + names[index] + ":PASS");
            Invoke(Find(catalog, "Deactivate", ControlType.Button, false));
            Named(results, "Inactive");
            Wait(delegate { return OwnedEditorValue(editors[current]) == historical; }, "Inactive historical selection was not retained.");
            Console.WriteLine("HISTORICAL_LOOKUP_RETAINED=" + names[index] + ":PASS");
        }
        Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
        page = Find(shell, "Goods Receipts", ControlType.TabItem, false);
        Invoke(Find(page, "New", ControlType.Button, false));
        Invoke(Find(page, "Add line", ControlType.Button, false));
        for (var index = 0; index < names.Length; index++)
        {
            var current = index;
            _stage = "inactive-new-selection-" + names[index];
            Console.WriteLine("STAGE=" + _stage + ";MS=" + TraceClock.ElapsedMilliseconds);
            SetInput(page, names[index], codes[index], false);
            var lookup = names[index] == "Product" ? InlineCell(page, names[index]) : Find(page, names[index], ControlType.ComboBox, false);
            SendOwnedLookupKey(InputEditor(page, names[index]), 0x73); // F4 opens without toggling an already-open popup.
            TraceLookupState(page, names[index], "inactive-open-posted", null);
            VisibleOwnedText("0 / 0");
            Console.WriteLine("INACTIVE_POPUP_EMPTY=" + names[index] + ";MS=" + TraceClock.ElapsedMilliseconds);
            if (names[index] == "Product")
            {
                var inline = InputEditor(page, "Product");
                SendOwnedLookupKey(inline, 27);
                SendOwnedLookupKey(inline, 27);
            }
            InputEditor(page, "Note").SetFocus();
            Wait(delegate { return names[current] == "Product" ? lookup.Current.HelpText.Contains("Committed key: 0") : lookup.Current.HelpText == "No selection"; }, "Inactive catalog became selectable in a new draft.");
        }
        SetInput(page, "Note", "Inactive references must not persist", false);
        Invoke(Find(page, "Save", ControlType.Button, false));
        Find(page, "The request is invalid.", ControlType.Text, false);
        Console.WriteLine("INACTIVE_NEW_SELECTION_REJECTED=PASS");
        CloseApplication(shell);
        _application.Dispose();
        _stage = "changed-lookup-real-reopen";
        StartApplication(executable);
        shell = Login();
        Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
        page = Find(shell, "Goods Receipts", ControlType.TabItem, false);
        Invoke(Find(page, receiptAction, ControlType.Button, false));
        Find(page, "Note", ControlType.Edit, false);
        Wait(delegate { return InputValue(page, "Note") == "GUI edited"; }, "Saved historical receipt did not reopen.");
        for (var index = 0; index < names.Length; index++)
        {
            var current = index;
            Wait(delegate { return InputValue(page, names[current]) == codes[current] + " - UIA " + names[current] + " Changed"; }, "Reopen did not fetch fresh catalog names.");
        }
        CloseApplication(shell);
        Console.WriteLine("CHANGED_LOOKUPS_REAL_REOPEN=PASS");
    }

    private static string OwnedEditorValue(IntPtr handle)
    {
        uint process;
        GetWindowThreadProcessId(handle, out process);
        if (process != _application.Id) throw new InvalidOperationException("Historical editor ownership changed.");
        return ((ValuePattern)AutomationElement.FromHandle(handle).GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
    }

    private static void VisibleOwnedText(string name)
    {
        Wait(delegate
        {
            var found = new System.Collections.Generic.List<AutomationElement>();
            var roots = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, _application.Id));
            foreach (AutomationElement root in roots)
                foreach (AutomationElement item in root.FindAll(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text), new PropertyCondition(AutomationElement.NameProperty, name))))
                {
                    if (item.Current.IsOffscreen) continue;
                    if (!found.Exists(delegate(AutomationElement prior) { return Automation.Compare(prior, item); })) found.Add(item);
                }
            if (found.Count > 1) throw new InvalidOperationException("Ambiguous visible popup status.");
            return found.Count == 1;
        }, "Visible lookup result status unavailable.");
    }

    private static void ReadOnlyValue(AutomationElement root, string expected)
    {
        Wait(delegate
        {
            var found = 0;
            foreach (AutomationElement cell in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsValuePatternAvailableProperty, true)))
            {
                var value = (ValuePattern)cell.GetCurrentPattern(ValuePattern.Pattern);
                if (value.Current.Value != expected) continue;
                if (!value.Current.IsReadOnly) throw new InvalidOperationException("Stock cell is editable.");
                found++;
            }
            if (found > 1) throw new InvalidOperationException("Ambiguous stock result value.");
            return found == 1;
        }, "Expected readonly stock value unavailable.");
    }

    private static bool PostWithSecondClient(AutomationElement page, AutomationElement post, string executable, string scenario, string receiptAction)
    {
        var firstClient = _application;
        var firstWindow = MainWindow;
        StartApplication(executable);
        var otherClient = _application;
        var otherShell = Login();
        var otherWindow = MainWindow;
        Invoke(Find(otherShell, "Goods Receipts", ControlType.Button, false));
        var otherPage = Find(otherShell, "Goods Receipts", ControlType.TabItem, false);
        Invoke(Find(otherPage, receiptAction, ControlType.Button, false));
        Find(otherPage, "Note", ControlType.Edit, false);
        Wait(delegate { return InputValue(otherPage, "Note") == "GUI edited"; }, "Second client did not load the saved draft.");
        var otherPost = Find(otherPage, "Post", ControlType.Button, false);
        Wait(delegate { return otherPost.Current.IsEnabled; }, "Second client did not receive the Draft version.");
        if (scenario == "inventory-stale")
        {
            _stage = "receipt-concurrent-winner";
            SetInput(otherPage, "Note", "GUI concurrent winner", false);
            Invoke(Find(otherPage, "Save", ControlType.Button, false));
            Find(otherShell, "Goods receipt saved.", ControlType.Text, false);
            Wait(delegate { return otherPost.Current.IsEnabled; }, "Concurrent save did not settle.");
            _application = firstClient;
            MainWindow = firstWindow;
            _stage = "receipt-stale-save";
            SetInput(page, "Note", "GUI stale losing input", false);
            Invoke(Find(page, "Save", ControlType.Button, false));
            Find(page, "The goods receipt was changed by another operation.", ControlType.Text, true);
            if (InputValue(page, "Note") != "GUI stale losing input") throw new InvalidOperationException("Stale edit was lost.");
            _application = otherClient;
            MainWindow = otherWindow;
            CloseApplication(otherShell);
            otherClient.Dispose();
            _application = firstClient;
            MainWindow = firstWindow;
            Console.WriteLine("STALE_RECEIPT_REJECTED_INPUT_RETAINED=PASS");
            return false;
        }
        _application = firstClient;
        MainWindow = firstWindow;
        _stage = "receipt-post";
        Invoke(post);
        Find(page, "Posted", ControlType.Text, false);
        Wait(delegate { return !post.Current.IsEnabled; }, "Posted receipt still permits Post.");
        Console.WriteLine("POST_BUTTON_DUPLICATE_DISABLED=PASS");
        _stage = "receipt-server-duplicate-post";
        _application = otherClient;
        MainWindow = otherWindow;
        Invoke(otherPost); // This independent client still holds the original Draft version.
        Find(otherPage, "The goods receipt is already posted.", ControlType.Pane, true);
        CloseApplication(otherShell);
        otherClient.Dispose();
        _application = firstClient;
        MainWindow = firstWindow;
        Console.WriteLine("SERVER_DUPLICATE_POST_REJECTED=PASS");
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputData
    {
        [FieldOffset(0)] public NativeKeyboardInput Keyboard;
        [FieldOffset(0)] public NativeMouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput { public uint Type; public NativeInputData Data; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    private static void SendOwnedKey(AutomationElement expectedFocus, ushort key)
    {
        foreach (var modifier in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
            if ((GetAsyncKeyState(modifier) & 0x8000) != 0)
                throw new InvalidOperationException("Native keyboard audit requires released modifier keys.");
        if (GetForegroundWindow() != MainWindow || expectedFocus.Current.ProcessId != _application.Id || !expectedFocus.Current.HasKeyboardFocus)
            throw new InvalidOperationException("Native keyboard audit lost its owned focus.");
        var input = new[] {
            new NativeInput { Type = 1, Data = new NativeInputData { Keyboard = new NativeKeyboardInput { Key = key } } },
            new NativeInput { Type = 1, Data = new NativeInputData { Keyboard = new NativeKeyboardInput { Key = key, Flags = 2 } } }
        };
        if (SendInput(2, input, Marshal.SizeOf(typeof(NativeInput))) != 2)
            throw new InvalidOperationException("Native keyboard input was not inserted.");
    }

    private static void AssertRecoveryKeyboardRoute(AutomationElement page)
    {
        _stage = "uncertain-native-tab-route";
        var status = Find(page, "Back to receipts", ControlType.Button, false);
        var refresh = Find(page, "Refresh", ControlType.Button, false);
        FocusOwnedEditor(status, "receipt recovery Tab");
        SendOwnedKey(status, 9);
        Thread.Sleep(300);
        var focused = AutomationElement.FocusedElement;
        if (focused.Current.ProcessId != _application.Id || GetForegroundWindow() != MainWindow)
            throw new InvalidOperationException("Native Tab focus escaped its owned application.");
        Console.WriteLine("RECOVERY_TAB_ACTUAL_FOCUS=" + focused.Current.Name + ";TYPE=" + focused.Current.ControlType.ProgrammaticName);
        Wait(delegate { return refresh.Current.HasKeyboardFocus; }, "Native Tab from Back skipped Refresh.", 5);
        Console.WriteLine("RECOVERY_NATIVE_TAB_REACHES_REFRESH=PASS");

    }

    private static void PostAfterUncertainOutcome(AutomationElement page, AutomationElement post)
    {
        _stage = "real-sql-post-timeout";
        Signal("posting");
        AwaitSignal("allow-post");
        Invoke(post);
        try { Find(page, "Posting outcome unknown.", ControlType.Pane, true, 45); }
        catch (TimeoutException) { Dump(page); throw; }
        foreach (var name in new[] { "Save", "Post", "New" })
            if (Find(page, name, ControlType.Button, false).Current.IsEnabled)
                throw new InvalidOperationException("Uncertain Post permits a mutation.");
        if (InputEditor(page, "Note").Current.IsEnabled) throw new InvalidOperationException("Uncertain editor remained editable.");
        AssertRecoveryKeyboardRoute(page);
        Signal("uncertain");
        AwaitSignal("allow-reload");
        _stage = "explicit-outcome-reload";
        SendOwnedKey(Find(page, "Refresh", ControlType.Button, false), 13);
        Console.WriteLine("RECOVERY_NATIVE_ENTER_SENT=PASS");
        Find(page, "Draft", ControlType.Text, false);
        Wait(delegate { return post.Current.IsEnabled; }, "Explicit reload did not restore the persisted Draft.");
        Signal("reloaded");
        AwaitSignal("allow-retry");
        _stage = "explicit-post-after-reload";
        Invoke(post);
        Find(page, "Posted", ControlType.Text, false);
        Console.WriteLine("UNCERTAIN_POST_RELOAD_EXPLICIT_RETRY=PASS");
    }

    private static void Signal(string name)
    {
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_BARRIER"), name), name);
    }

    private static void AwaitSignal(string name)
    {
        Wait(delegate { return File.Exists(Path.Combine(Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_BARRIER"), name)); }, "Owned test coordination did not release " + name);
    }

    private static void ObserveAttendedPhase(AutomationElement shell, AutomationElement page, string barrier, string phase, string[] names, string completion)
    {
        _stage = "bcf-attended-human-" + phase;
        File.WriteAllText(Path.Combine(barrier, "ready"), "phase=" + phase + ";" + NativeDescription(Find(shell, "Theme toggle", ControlType.Button, false)));
        var observedLight = false;
        var observedDark = false;
        // No query keys or value assignment during this phase. Markers request native
        // snapshots; they do not certify the human's per-key outcome or grant approval.
        Wait(delegate
        {
            if (File.Exists(Path.Combine(barrier, "observe")))
            {
                if (File.ReadAllText(Path.Combine(barrier, "observe")) != phase)
                    throw new InvalidOperationException("Attended observation phase mismatch.");
                var currentTheme = NativeDescription(Find(shell, "Theme toggle", ControlType.Button, false));
                if (currentTheme != "Current theme: Light" && currentTheme != "Current theme: Dark")
                    throw new InvalidOperationException("Unknown attended theme.");
                Console.WriteLine("ATTENDED_OBSERVED_PHASE=" + phase + ";" + currentTheme);
                foreach (var name in names)
                {
                    var edit = InputEditor(page, name);
                    var handle = new IntPtr(edit.Current.NativeWindowHandle);
                    uint process;
                    GetWindowThreadProcessId(handle, out process);
                    if (handle == IntPtr.Zero || process != _application.Id || edit.Current.IsOffscreen)
                        throw new InvalidOperationException("Attended visible EDIT ownership mismatch.");
                    int selectionStart, selectionEnd;
                    SendMessageSelection(handle, 0x00B0, out selectionStart, out selectionEnd);
                    Console.WriteLine("ATTENDED_SNAPSHOT=" + phase + ";" + name + ";RAW=" + ((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).Current.Value +
                        ";RANGE=" + selectionStart + "+" + (selectionEnd - selectionStart) + ";FOCUS=" + edit.Current.HasKeyboardFocus);
                }
                observedLight |= currentTheme == "Current theme: Light";
                observedDark |= currentTheme == "Current theme: Dark";
                File.Delete(Path.Combine(barrier, "observe"));
                File.WriteAllText(Path.Combine(barrier, "observed"), "phase=" + phase + ";" + currentTheme);
            }
            return observedLight && observedDark && File.Exists(Path.Combine(barrier, completion));
        }, "Attended " + phase + " walkthrough was not completed within its owned budget.", 600);
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
        var lookupInput = false;
        foreach (AutomationElement candidate in candidates)
            if (candidate.Current.ControlType == ControlType.ComboBox) lookupInput = true;
        if (lookupInput) FocusOwnedEditor(editor, name);
        else editor.SetFocus(); // Composite numeric ValuePattern providers need not focus their semantic wrapper.
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
        if (IsLineField(name) && LineGrid(root) != null && name != "Product")
            SendOwnedLookupKey(editor, 13);
        Console.WriteLine("INPUT_PATTERN=" + name + ":Value");
    }

    private static void SetLookup(AutomationElement page, string name, string display)
    {
        SetInput(page, name, display.Substring(0, display.IndexOf(" - ", StringComparison.Ordinal)), false);
        var lookup = name == "Product" ? InlineCell(page, name) : Find(page, name, ControlType.ComboBox, false);
        var editor = InputEditor(page, name);
        TraceLookupState(page, name, "before-open", display);
        SendOwnedLookupKey(editor, 0x73); // Typing may already have opened results; F4 is idempotent.
        TraceLookupState(page, name, "after-open-posted", display);
        var selectedPopup = IntPtr.Zero;
        try { Wait(delegate
        {
            var found = 0;
            var roots = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, _application.Id));
            foreach (AutomationElement popup in roots)
            {
                // Cache only non-password candidates; one provider snapshot avoids per-cell cross-process reads.
                var cache = new CacheRequest();
                cache.Add(AutomationElement.NameProperty);
                cache.Add(ValuePattern.Pattern);
                cache.Add(ValuePattern.ValueProperty);
                AutomationElementCollection cells;
                using (cache.Activate())
                    cells = popup.FindAll(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.IsPasswordProperty, false),
                        new PropertyCondition(AutomationElement.IsOffscreenProperty, false),
                        new OrCondition(new PropertyCondition(AutomationElement.NameProperty, display),
                            new PropertyCondition(AutomationElement.IsValuePatternAvailableProperty, true))));
                foreach (AutomationElement cell in cells)
                {
                    object pattern;
                    if (cell.Cached.Name != display &&
                        (!cell.TryGetCachedPattern(ValuePattern.Pattern, out pattern) || ((ValuePattern)pattern).Cached.Value != display)) continue;
                    var popupWindow = OwnedPopupWindow(cell);
                    if (popupWindow == IntPtr.Zero) continue; // A matching main-editor value is not a popup result.
                    selectedPopup = popupWindow;
                    found++;
                }
            }
            if (found > 1) throw new InvalidOperationException("Ambiguous lookup result.");
            return found == 1;
        }, "Unique lookup result not available: " + name); }
        catch
        {
            TraceLookupState(page, name, "result-failed", display);
            var roots = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, _application.Id));
            foreach (AutomationElement root in roots)
            {
                Console.WriteLine("LOOKUP_ROOT=" + root.Current.ControlType.ProgrammaticName + ":" + root.Current.Name);
                foreach (AutomationElement child in root.FindAll(TreeScope.Descendants, Condition.TrueCondition))
                    if (!child.Current.IsPassword && (child.Current.Name.Contains("GUI") || child.Current.ControlType == ControlType.DataGrid || child.Current.ControlType == ControlType.Table))
                    {
                        Console.WriteLine("LOOKUP_ITEM=" + child.Current.ControlType.ProgrammaticName + ":" + child.Current.Name);
                        foreach (var pattern in child.GetSupportedPatterns()) Console.WriteLine("LOOKUP_PATTERN=" + pattern.ProgrammaticName);
                    }
            }
            throw;
        }
        SendOwnedLookupKey(editor, 13);
        try { Wait(delegate { return lookup.Current.HelpText.Contains("Selected: " + display); }, "Lookup selection did not commit: " + name);
            Wait(delegate { return !IsWindowVisible(selectedPopup); }, "Committed lookup popup did not close: " + name);
            Console.WriteLine("LOOKUP_COMMITTED_POPUP_CLOSED=" + name + ";MS=" + TraceClock.ElapsedMilliseconds);
            TraceLookupState(page, name, "committed", display); }
        catch {
            Console.WriteLine("LOOKUP_HELP=" + lookup.Current.HelpText + ";TEXT=" + InputValue(page, name));
            Console.WriteLine("LOOKUP_FOCUS=" + InputEditor(page, name).Current.HasKeyboardFocus + ";NOTE_FOCUS=" + InputEditor(page, "Note").Current.HasKeyboardFocus);
            throw;
        }
    }

    private static IntPtr OwnedPopupWindow(AutomationElement result)
    {
        if (MainWindow == IntPtr.Zero) throw new InvalidOperationException("Owned main-window identity is unavailable.");
        var depth = 0;
        for (var node = result; node != null && depth++ < 32; node = TreeWalker.ControlViewWalker.GetParent(node))
        {
            var handle = new IntPtr(node.Current.NativeWindowHandle);
            if (handle == IntPtr.Zero) continue;
            var window = GetAncestor(handle, 2); // GA_ROOT, from the actual result cell's native ancestry.
            uint process;
            GetWindowThreadProcessId(window, out process);
            if (process != _application.Id) throw new InvalidOperationException("Lookup result is not an owned window.");
            return window == MainWindow ? IntPtr.Zero : window;
        }
        return IntPtr.Zero;
    }
    private static void TraceLookupState(AutomationElement page, string name, string phase, string expected)
    {
        if (Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_TRACE_LOOKUPS") != "1") return;
        try
        {
            var editor = InputEditor(page, name);
            uint foregroundProcess;
            GetWindowThreadProcessId(GetForegroundWindow(), out foregroundProcess);
            Console.WriteLine("LOOKUP_TRACE=" + phase + ";NAME=" + name + ";MS=" + TraceClock.ElapsedMilliseconds +
                ";FOREGROUND_OWNED=" + (foregroundProcess == _application.Id) +
                ";FOCUSED=" + editor.Current.HasKeyboardFocus + ";ENABLED=" + editor.Current.IsEnabled +
                ";OFFSCREEN=" + editor.Current.IsOffscreen + ";BOUNDS=" + editor.Current.BoundingRectangle +
                ";TEXT=" + InputValue(page, name));
            var roots = AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, _application.Id));
            foreach (AutomationElement root in roots)
            {
                if (root.Current.ControlType == ControlType.Window && root.Current.Name == "MyDmsVn") continue;
                var grid = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid));
                var expectedRows = expected == null ? -1 : root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, expected)).Count;
                Console.WriteLine("LOOKUP_POPUP=" + phase + ";NAME=" + name + ";TYPE=" + root.Current.ControlType.ProgrammaticName +
                    ";OFFSCREEN=" + root.Current.IsOffscreen + ";GRID=" + (grid != null) + ";EXPECTED_ROWS=" + expectedRows);
            }
        }
        catch (Exception error) { Console.WriteLine("LOOKUP_TRACE_ERROR=" + error.GetType().Name); }
    }
    private static void SendOwnedLookupKey(AutomationElement editor, int key)
    {
        FocusOwnedEditor(editor);
        var handle = new IntPtr(editor.Current.NativeWindowHandle);
        if (!PostMessage(handle, 0x100, new IntPtr(key), new IntPtr(1)) ||
            !PostMessage(handle, 0x101, new IntPtr(key), new IntPtr(unchecked((int)0xC0000001))))
            throw new InvalidOperationException("Owned lookup keyboard input failed.");
    }

    private static void FocusOwnedEditor(AutomationElement editor, string input = "lookup keyboard")
    {
        var handle = new IntPtr(editor.Current.NativeWindowHandle);
        var window = GetAncestor(handle, 2);
        uint editorProcess, windowProcess;
        GetWindowThreadProcessId(handle, out editorProcess);
        GetWindowThreadProcessId(window, out windowProcess);
        if (handle == IntPtr.Zero || window == IntPtr.Zero ||
            editorProcess != _application.Id || windowProcess != _application.Id)
            throw new InvalidOperationException("Input target is not an owned editor/window.");
        // One activation request, never an input-queue attachment or repeated business action.
        if (IsIconic(window)) ShowWindowAsync(window, 9); // Restore only this owned minimized window.
        var activationRequested = GetForegroundWindow() != window;
        var activationGranted = !activationRequested || SetForegroundWindow(window);
        try
        {
            // Cross-input-queue activation completes asynchronously; do not race UIA focus against it.
            Wait(delegate { return GetForegroundWindow() == window; },
                "Owned input window did not receive foreground before keyboard focus.", 5);
            editor.SetFocus();
            Wait(delegate { return GetForegroundWindow() == window && editor.Current.HasKeyboardFocus; },
                "Owned input window/editor did not receive foreground and keyboard focus.");
        }
        catch (TimeoutException)
        {
            // Metadata only: no editor values, credentials, window titles or external process identifiers.
            try
            {
                uint foregroundProcess;
                var foreground = GetForegroundWindow();
                GetWindowThreadProcessId(foreground, out foregroundProcess);
                Console.WriteLine("OWNED_FOCUS_FAILED=" + input + ";FOREGROUND_OWNED=" +
                    (foregroundProcess == _application.Id) + ";ROOT_FOREGROUND=" + (foreground == window) +
                    ";EDITOR_FOCUSED=" + editor.Current.HasKeyboardFocus +
                    ";ACTIVATION_REQUESTED=" + activationRequested + ";ACTIVATION_GRANTED=" + activationGranted);
            }
            catch (ElementNotAvailableException) { Console.WriteLine("OWNED_FOCUS_FAILED=element unavailable"); }
            throw;
        }
    }


    private static bool IsLineField(string name) { return name == "Product" || name == "Quantity" || name == "Unit cost" || name == "Unit price"; }
    private static AutomationElement LineGrid(AutomationElement root)
    {
        return root.FindFirst(TreeScope.Descendants, new AndCondition(
            new OrCondition(new PropertyCondition(AutomationElement.NameProperty, "Goods receipt lines"),
                new PropertyCondition(AutomationElement.NameProperty, "Sales receipt lines")),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Table)));
    }
    private static AutomationElement InlineCell(AutomationElement root, string field)
    {
        var grid = LineGrid(root);
        if (grid == null) throw new InvalidOperationException("Receipt line table absent.");
        AutomationElement selected = null;
        foreach (AutomationElement candidate in grid.FindAll(TreeScope.Descendants, Condition.TrueCondition))
        {
            var name = candidate.Current.Name;
            if (!name.StartsWith(field + " row ", StringComparison.Ordinal) || name.EndsWith(" editor", StringComparison.Ordinal)) continue;
            if (grid.Current.Name == "Sales receipt lines" && name != field + " row " + _salesRow) continue;
            if (selected != null) throw new InvalidOperationException("Ambiguous receipt line field: " + field);
            selected = candidate;
        }
        if (selected == null) throw new InvalidOperationException("Receipt line field absent: " + field);
        return selected;
    }

    private static bool TrySalesCell(AutomationElement page, string field, out AutomationElement cell)
    {
        try { cell = InlineCell(page, field); return true; }
        catch (InvalidOperationException error)
        {
            if (!error.Message.StartsWith("Receipt line field absent:", StringComparison.Ordinal) &&
                error.Message != "Receipt line table absent.") throw;
            cell = null;
            return false;
        }
    }
    private static AutomationElement InlineEditor(AutomationElement root, string field)
    {
        var cell = InlineCell(root, field);
        var name = cell.Current.Name + " editor";
        var wrapper = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        if (wrapper == null)
        {
            ((InvokePattern)cell.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            Wait(delegate {
                wrapper = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
                return wrapper != null;
            }, "Active inline editor unavailable: " + field);
        }
        var input = wrapper.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        if (input == null || input.Current.NativeWindowHandle == 0) throw new InvalidOperationException("Native inline textbox absent.");
        return input;
    }
    private static AutomationElement InputEditor(AutomationElement root, string name)
    {
        if (IsLineField(name) && LineGrid(root) != null) return InlineEditor(root, name);
        var key = _application.Id + ":" + string.Join(".", root.GetRuntimeId()) + ":" + name;
        CachedInput cached;
        if (InputEditors.TryGetValue(key, out cached))
        {
            try
            {
                // Cache widget identity only; values and all grid/result assertions remain live.
                if (cached.Named.Current.ProcessId == _application.Id && cached.Named.Current.Name == name &&
                    cached.Editor.Current.ProcessId == _application.Id && cached.Editor.Current.NativeWindowHandle != 0)
                    return cached.Editor;
            }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
            InputEditors.Remove(key);
        }
        AutomationElement editor = null;
        AutomationElement semantic = null;
        var candidates = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, name));
        foreach (AutomationElement named in candidates)
        {
            // Captions/groups can inherit the same name but contain an unrelated EDIT.
            // Resolve only semantic input widgets; ambiguity between real inputs still fails.
            var type = named.Current.ControlType;
            if (type != ControlType.Edit && type != ControlType.ComboBox && type != ControlType.Spinner) continue;
            var candidate = named.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (candidate == null && named.Current.ControlType == ControlType.Edit) candidate = named;
            if (candidate == null) continue;
            if (editor != null && !Automation.Compare(editor, candidate))
            {
                _detail = "Ambiguous input selector: " + name + ";first=" + editor.Current.ControlType.ProgrammaticName +
                    ";next=" + candidate.Current.ControlType.ProgrammaticName +
                    ";sameNativeHandle=" + (editor.Current.NativeWindowHandle == candidate.Current.NativeWindowHandle) +
                    ";semantic=" + named.Current.ControlType.ProgrammaticName;
                throw new InvalidOperationException();
            }
            editor = candidate;
            semantic = named;
        }
        if (editor == null) { _detail = "Input unavailable: " + name; throw new InvalidOperationException(); }
        InputEditors[key] = new CachedInput { Named = semantic, Editor = editor };
        return editor;
    }
    private static string InputValue(AutomationElement root, string name)
    {
        if (IsLineField(name) && LineGrid(root) != null)
        {
            var cell = InlineCell(root, name);
            if (name != "Product") return ((ValuePattern)cell.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
            var help = cell.Current.HelpText;
            var marker = "Selected: ";
            var start = help.IndexOf(marker, StringComparison.Ordinal);
            return start < 0 ? string.Empty : help.Substring(start + marker.Length);
        }
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
        for (var index = 0; index < children.Count && index < 160; index++)
        {
            var item = children[index];
            if (item.Current.IsPassword) continue;
            Console.WriteLine("UIA=" + item.Current.ControlType.ProgrammaticName + ":" + item.Current.Name);
        }
    }

    private static AutomationElement Window(string title, int seconds = 15)
    {
        AutomationElement result = null;
        Wait(delegate
        {
            var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, _application.Id));
            foreach (AutomationElement candidate in windows)
                if (candidate.Current.Name == title) { result = candidate; return true; }
            return false;
        }, "Owned window unavailable.", seconds);
        var handle = new IntPtr(result.Current.NativeWindowHandle);
        if (title == "MyDmsVn") MainWindow = handle;
        // Effective DPI is measured independently of the target window's virtualized DPI.
        uint dpiX, dpiY;
        var dpiResult = GetDpiForMonitor(MonitorFromWindow(handle, 2), 0, out dpiX, out dpiY);
        if (dpiResult != 0 || dpiX != dpiY) throw new InvalidOperationException("Monitor effective DPI unavailable.");
        var scale = dpiX * 100m / 96m;
        Console.WriteLine("MONITOR_SCALE=" + scale + ";WINDOW_DPI=" + GetDpiForWindow(handle) + ";MONITOR_DPI=" + dpiX);
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_EXPECTED_SCALE") ?? "100";
        if ((expected != "100" && expected != "125") || scale.ToString(System.Globalization.CultureInfo.InvariantCulture) != expected)
            throw new InvalidOperationException("Desktop scale differs from declared configuration.");
        return result;
    }

    private static AutomationElement Find(AutomationElement root, string name, ControlType type, bool contains, int seconds = 15)
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
        }, "Required accessible element unavailable.", seconds);
        return result;
    }

    private static void Invoke(AutomationElement element)
    {
        Wait(delegate { return element.Current.IsEnabled; }, "Action stayed disabled.");
        ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static void Wait(Func<bool> condition, string failure, int seconds = 15)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            try { if (condition()) return; }
            catch (ElementNotAvailableException) { }
            Thread.Sleep(25); // Finite predicate polling; never a guessed UI delay.
        }
        _detail = failure;
        throw new TimeoutException();
    }
}
