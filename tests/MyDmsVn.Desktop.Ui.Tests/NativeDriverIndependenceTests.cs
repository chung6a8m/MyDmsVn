using System;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Formatting;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class NativeDriverIndependenceTests
{
    [Fact]
    public void Linked_driver_routes_native_key_without_goods_receipt_fixture()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            using var form = new Form { Width = 400, Height = 180 };
            using var date = new BootstrapDateInput
            {
                Dock = DockStyle.Top,
                AccessibleName = "Date pilot",
                Profile = new DateInputProfile(false, DateTimePicker.MinimumDateTime, DateTimePicker.MaximumDateTime),
                ReferenceDateProvider = () => new DateTime(2026, 10, 22),
            };
            form.Controls.Add(date);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            date.Text = "12/10/2025";
            var driver = new NativeLookupTypingDriver(date);
            driver.SelectRange(4, 0);
            driver.Key(Keys.Up);
            Assert.Equal("12/11/2025", date.PendingText);
            Assert.Equal(4, driver.SelectionStart);
            Assert.Equal(0, driver.SelectionLength);
            driver.AssertFocused();
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(15));
    }
}
