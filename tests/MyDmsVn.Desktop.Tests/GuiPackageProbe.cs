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
    private static Process _application;
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
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);

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
            else if (args[1] == "invalid-audit")
            {
                var shell = Login();
                _stage = "receipt-invalid-fields";
                Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
                var page = Find(shell, "Goods Receipts", ControlType.TabItem, false);
                Invoke(Find(page, "Add line", ControlType.Button, false));
                SetInput(page, "Quantity", 0m.ToString("F4"), false);
                SetInput(page, "Note", "GUI invalid input retained", false);
                Invoke(Find(page, "Save", ControlType.Button, false));
                Find(page, "The request is invalid.", ControlType.Text, false);
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
                Find(shell, "The operation is not permitted.", ControlType.Text, true);
                if (InputValue(page, "Code") != "GUI-DENY") throw new InvalidOperationException("Denied edit was lost.");
                CloseApplication(shell);
                Console.WriteLine("SERVER_DIRECT_DENY=PASS");
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
                if (args[1] == "inventory-audit" || args[1] == "inventory-fault") Console.WriteLine("RECEIPT_POST_STOCK=PASS");
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
        var row = Named(results, "Result " + code);
        var id = row.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.HelpTextProperty, "ID"));
        if (id == null) throw new InvalidOperationException("Catalog ID column inaccessible.");
        CatalogIds[title] = int.Parse(id.Current.Name, System.Globalization.CultureInfo.InvariantCulture);
        Console.WriteLine("CATALOG_FLOW=" + title + ":PASS");
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
        if (theme.Current.HelpText != "Current theme: " + _theme) Invoke(theme);
        Wait(delegate { return theme.Current.HelpText == "Current theme: " + _theme; }, "Theme metadata did not match the requested mode.");
        Console.WriteLine("APPLICATION_THEME=" + _theme);
        return shell;
    }

    private static void Receipt(AutomationElement shell, string executable, string scenario)
    {
        _stage = "receipt-draft";
        Invoke(Find(shell, "Goods Receipts", ControlType.Button, false));
        var page = Find(shell, "Goods Receipts", ControlType.TabItem, false);
        Dump(page);
        SetLookup(page, "Warehouse", "GUI-WH - UIA Warehouse Updated");
        SetLookup(page, "Employee", "GUI-EMP - UIA Employee Updated");
        SetInput(page, "Note", "GUI draft", false);
        Invoke(Find(page, "Add line", ControlType.Button, false));
        SetLookup(page, "Product", "GUI-PROD - UIA Product Updated");
        Dump(page);
        SetInput(page, "Quantity", 2.5m.ToString("F4"), false);
        SetInput(page, "Unit cost", 4m.ToString("F4"), false);
        InputEditor(page, "Note").SetFocus();
        Find(page, 10m.ToString("F2"), ControlType.Text, false);
        Invoke(Find(page, "Save", ControlType.Button, false));
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
        Invoke(Find(results, receiptAction, ControlType.Button, false));
        Wait(delegate { return InputValue(page, "Note") == "GUI draft"; }, "Saved draft did not reload.");
        _stage = "receipt-edit";
        SetInput(page, "Note", "GUI edited", false);
        Invoke(Find(page, "Open GUI-PROD - UIA Product Updated", ControlType.Button, false));
        SetInput(page, "Quantity", 3.125m.ToString("F4"), false);
        InputEditor(page, "Note").SetFocus();
        Find(page, 12.5m.ToString("F2"), ControlType.Text, false);
        Invoke(Find(page, "Save", ControlType.Button, false));
        var post = Find(page, "Post", ControlType.Button, false);
        Wait(delegate { return post.Current.IsEnabled; }, "Saved draft cannot be posted.");
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
            _stage = "inactive-new-selection-" + names[index];
            SetInput(page, names[index], codes[index], false);
            var lookup = Find(page, names[index], ControlType.ComboBox, false);
            Invoke(Find(lookup, "Open lookup results", ControlType.Button, false));
            VisibleOwnedText("0 / 0");
            InputEditor(page, "Note").SetFocus();
            Wait(delegate { return lookup.Current.HelpText == "No selection"; }, "Inactive catalog became selectable in a new draft.");
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
        StartApplication(executable);
        var otherClient = _application;
        var otherShell = Login();
        Invoke(Find(otherShell, "Goods Receipts", ControlType.Button, false));
        var otherPage = Find(otherShell, "Goods Receipts", ControlType.TabItem, false);
        Invoke(Find(otherPage, receiptAction, ControlType.Button, false));
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
            _stage = "receipt-stale-save";
            SetInput(page, "Note", "GUI stale losing input", false);
            Invoke(Find(page, "Save", ControlType.Button, false));
            Find(page, "The goods receipt was changed by another operation.", ControlType.Text, true);
            if (InputValue(page, "Note") != "GUI stale losing input") throw new InvalidOperationException("Stale edit was lost.");
            _application = otherClient;
            CloseApplication(otherShell);
            otherClient.Dispose();
            _application = firstClient;
            Console.WriteLine("STALE_RECEIPT_REJECTED_INPUT_RETAINED=PASS");
            return false;
        }
        _application = firstClient;
        _stage = "receipt-post";
        Invoke(post);
        Find(page, "Posted", ControlType.Text, false);
        Wait(delegate { return !post.Current.IsEnabled; }, "Posted receipt still permits Post.");
        Console.WriteLine("POST_BUTTON_DUPLICATE_DISABLED=PASS");
        _stage = "receipt-server-duplicate-post";
        _application = otherClient;
        Invoke(otherPost); // This independent client still holds the original Draft version.
        Find(otherPage, "The goods receipt is already posted.", ControlType.Text, true);
        CloseApplication(otherShell);
        otherClient.Dispose();
        _application = firstClient;
        Console.WriteLine("SERVER_DUPLICATE_POST_REJECTED=PASS");
        return true;
    }

    private static void PostAfterUncertainOutcome(AutomationElement page, AutomationElement post)
    {
        _stage = "real-sql-post-timeout";
        Signal("posting");
        AwaitSignal("allow-post");
        Invoke(post);
        Find(page, "Posting outcome unknown.", ControlType.Text, true, 45);
        foreach (var name in new[] { "Save", "Post", "New" })
            if (Find(page, name, ControlType.Button, false).Current.IsEnabled)
                throw new InvalidOperationException("Uncertain Post permits a mutation.");
        if (InputEditor(page, "Note").Current.IsEnabled) throw new InvalidOperationException("Uncertain editor remained editable.");
        Signal("uncertain");
        AwaitSignal("allow-reload");
        _stage = "explicit-outcome-reload";
        Invoke(Find(page, "Refresh", ControlType.Button, false));
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
        editor.SetFocus();
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

    private static void SetLookup(AutomationElement page, string name, string display)
    {
        SetInput(page, name, display.Substring(0, display.IndexOf(" - ", StringComparison.Ordinal)), false);
        var lookup = Find(page, name, ControlType.ComboBox, false);
        Invoke(Find(lookup, "Open lookup results", ControlType.Button, false));
        var editor = InputEditor(page, name);
        var handle = new IntPtr(editor.Current.NativeWindowHandle);
        var shell = GetAncestor(handle, 2);
        try { Wait(delegate
        {
            var found = 0;
            var roots = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, _application.Id));
            foreach (AutomationElement popup in roots)
            {
                var cells = popup.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                foreach (AutomationElement cell in cells)
                {
                    if (cell.Current.IsPassword) continue;
                    object pattern;
                    if (cell.Current.Name == display || (cell.TryGetCurrentPattern(ValuePattern.Pattern, out pattern) && ((ValuePattern)pattern).Current.Value == display)) found++;
                }
            }
            if (found > 1) throw new InvalidOperationException("Ambiguous lookup result.");
            return found == 1;
        }, "Unique lookup result not available: " + name); }
        catch
        {
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
        editor.SetFocus();
        uint process;
        GetWindowThreadProcessId(handle, out process);
        _detail = "Keyboard target must be the focused owned lookup editor.";
        if (process != _application.Id) throw new InvalidOperationException("Keyboard input target is not an owned editor.");
        Wait(delegate { return editor.Current.HasKeyboardFocus; }, "Owned lookup editor did not receive keyboard focus.");
        if (!PostMessage(handle, 0x100, new IntPtr(13), new IntPtr(1)) ||
            !PostMessage(handle, 0x101, new IntPtr(13), new IntPtr(unchecked((int)0xC0000001))))
            throw new InvalidOperationException("Owned lookup keyboard input failed.");
        try { Wait(delegate { return lookup.Current.HelpText == "Selected: " + display; }, "Lookup selection did not commit: " + name); }
        catch {
            Console.WriteLine("LOOKUP_HELP=" + lookup.Current.HelpText + ";TEXT=" + InputValue(page, name));
            Console.WriteLine("LOOKUP_FOCUS=" + InputEditor(page, name).Current.HasKeyboardFocus + ";NOTE_FOCUS=" + InputEditor(page, "Note").Current.HasKeyboardFocus);
            throw;
        }
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
        if (editor == null) { _detail = "Input unavailable: " + name; throw new InvalidOperationException(); }
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
