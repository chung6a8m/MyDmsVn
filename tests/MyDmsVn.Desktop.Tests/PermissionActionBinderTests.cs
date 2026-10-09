using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class PermissionActionBinderTests
    {
        [Fact]
        public void Action_stays_disabled_until_permission_is_granted()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var action = new Button { Enabled = true })
                    {
                        var binder = new PermissionActionBinder(
                            new StubPermissionApiClient(isAllowed: true));

                        binder.ApplyAsync(action, "Catalog.Products.Write", cancellationToken)
                            .GetAwaiter()
                            .GetResult();

                        Assert.True(action.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Action_is_disabled_when_permission_is_denied_or_cannot_be_verified()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var denied = new Button { Enabled = true })
                    using (var failed = new Button { Enabled = true })
                    {
                        new PermissionActionBinder(new StubPermissionApiClient(isAllowed: false))
                            .ApplyAsync(denied, "Catalog.Products.Write", cancellationToken)
                            .GetAwaiter()
                            .GetResult();
                        new PermissionActionBinder(new FailingPermissionApiClient())
                            .ApplyAsync(failed, "Catalog.Products.Write", cancellationToken)
                            .GetAwaiter()
                            .GetResult();

                        Assert.False(denied.Enabled);
                        Assert.False(failed.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private sealed class StubPermissionApiClient : IPermissionApiClient
        {
            private readonly bool _isAllowed;

            public StubPermissionApiClient(bool isAllowed)
            {
                _isAllowed = isAllowed;
            }

            public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    ApiResponse<PermissionDecisionDto>.Success(
                        new PermissionDecisionDto(permissionKey, _isAllowed)));
            }
        }

        private sealed class FailingPermissionApiClient : IPermissionApiClient
        {
            public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    ApiResponse<PermissionDecisionDto>.Failure(
                        new ApiError(
                            ApiStatusCode.InternalServerError,
                            "Permission.Unavailable",
                            "Permission could not be checked.")));
            }
        }
    }
}
