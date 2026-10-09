using System;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopHostRunnerTests
    {
        [Fact]
        public void Smoke_mode_creates_and_renders_the_shared_shell_without_a_message_loop()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new FoundationShellFormTests.ReadyApiClient())))
                    {
                        var exitCode = DesktopHostRunner.Run(shell, smokeTest: true);

                        Assert.Equal(0, exitCode);
                        Assert.True(shell.IsHandleCreated);
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }
    }
}
