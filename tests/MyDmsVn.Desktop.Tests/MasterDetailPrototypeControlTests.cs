using System;
using System.Collections.Generic;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class MasterDetailPrototypeControlTests
    {
        [Fact]
        public void SetRows_projects_master_rows_into_the_source_grid()
        {
            StaTest.Run(
                _ =>
                {
                    using (var control = new MasterDetailPrototypeControl())
                    {
                        control.SetRows(
                            new[]
                            {
                                new MasterDetailRow("1", "P001", "Sample item"),
                            });

                        Assert.Equal(2, control.Grid.RowsCount);
                        Assert.Equal("P001", control.Grid[1, 1].Value);
                        Assert.Equal("Sample item", control.Grid[1, 2].Value);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Lookup_and_field_errors_follow_reusable_contracts()
        {
            StaTest.Run(
                _ =>
                {
                    using (var control = new MasterDetailPrototypeControl())
                    {
                        control.SetLookupOptions(
                            new[]
                            {
                                new LookupOption(1, "North"),
                                new LookupOption(2, "South"),
                            });

                        var selected = control.Lookup.SelectValue(2);
                        control.ApplyFieldErrors(
                            new Dictionary<string, IReadOnlyList<string>>
                            {
                                ["code"] = new[] { "Code is required." },
                            });

                        Assert.True(selected);
                        Assert.Equal(2, control.Lookup.SelectedValue);
                        Assert.Equal("Code is required.", control.GetFieldError("code"));
                        Assert.Equal(string.Empty, control.GetFieldError("name"));
                    }
                },
                TimeSpan.FromSeconds(10));
        }
    }
}
