# Ý tưởng cho project

## Mục tiêu

Tạo MVP theo mô hình Desktop Client - Server có thể áp dụng cho qui mô SME (nhiều module). Với một số đặc tính sau:

- Sử dụng C#, target net48;net8.0, với khung tiêu chuẩn Clean Architecture.
- Desktop Client sử dụng WinForms.
- Server phát triển theo 2 roadmap:
  * v1: là mục tiêu chính, sử dụng SQL Server và code infrastructure mô phỏng Web API, Desktop Client sẽ references trực tiếp để hoạt động.
  * v2: phát hành Web API ASP.NET Core 8 chính thức để hoàn thiện Server.

## Các thư viện sẽ dùng

File Directory.Packages.props như sau:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    <NoWarn>$(NoWarn);NU1507</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="BCrypt.Net-Next" Version="4.2.0" />
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageVersion Include="Dapper" Version="2.1.79" />
    <PackageVersion Include="dbup-sqlserver" Version="7.2.0" />
    <PackageVersion Include="ErrorOr" Version="2.1.1" />
    <PackageVersion Include="FluentValidation" Version="11.12.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.12.0" />    
    <PackageVersion Include="MediatR" Version="12.5.0" />
    <PackageVersion Include="MediatR.Contracts" Version="2.0.1" />
    <PackageVersion Include="Microsoft.Data.SqlClient" Version="7.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageVersion Include="MiniExcel" Version="1.45.0" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
    <PackageVersion Include="Onova" Version="2.6.13" />
    <PackageVersion Include="RepoDb" Version="1.15.1" />
    <PackageVersion Include="RepoDb.SqlServer" Version="1.15.1" />
    <PackageVersion Include="RepoDb.SqlServer.BulkOperations" Version="1.15.0" />
    <PackageVersion Include="Serilog" Version="4.4.0" />
    <PackageVersion Include="Serilog.Extensions.Logging" Version="8.0.0" />
    <PackageVersion Include="Serilog.Sinks.File" Version="7.0.0" />
    <PackageVersion Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## Các project C# có thể có

- MyDmsVn.Contracts: Định hình models loại Request, Response, Dto (Data Object Transfer).
- MyDmsVn.SharedKernel: Dùng chung cho cả Desktop và Server.
- MyDmsVn.Desktop.Application: Abstractions cho các interface IxxxApiClient.
- MyDmsVn.Desktop.App: Host cho net48.
- MyDmsVn.Desktop.AppCore: Host cho net8.0.
- MyDmsVn.Desktop.Infrastructure: Cài đặt cho các xxxApiClient.
- MyDmsVn.Desktop.WinForms: Triểu khai UI dùng chung cho cả net48 và net8.0, sử dụng CommunityToolkit.Mvvm.
- MyDmsVn.Desktop.Shared: Phần dùng chung nếu cần.
- MyDmsVn.Server.Api: Host Web API (chỉ net8.0, chỉ roadmap v2).
- MyDmsVn.Server.Application
- MyDmsVn.Server.DbMigrator: nhúng Sql script để triển khai DbUp.
- MyDmsVn.Server.Domain
- MyDmsVn.Server.Infrastructure

## Phân quyền người sử dụng

Ưu tiên UserPermissions rồi xét đến UserRoles. Cấu trúc các bảng bằng SQL như sau:

```sql
CREATE TABLE [dbo].[Users](
	[UserId] [int] IDENTITY(1,1) NOT NULL,
	[Username] [nvarchar](100) NOT NULL,
	[DisplayName] [nvarchar](100) NOT NULL,
	[Email] [nvarchar](100) NULL,
	[Source] [nvarchar](4) NOT NULL,
	[PasswordHash] [nvarchar](86) NOT NULL,
	[PasswordSalt] [nvarchar](10) NOT NULL,
	[LastDirectoryUpdate] [datetime] NULL,
	[UserImage] [nvarchar](100) NULL,
	[InsertDate] [datetime] NOT NULL,
	[InsertUserId] [int] NOT NULL,
	[UpdateDate] [datetime] NULL,
	[UpdateUserId] [int] NULL,
	[IsActive] [smallint] NOT NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC)
)
GO

ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO

CREATE TABLE [dbo].[Roles](
	[RoleId] [int] IDENTITY(1,1) NOT NULL,
	[RoleName] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([RoleId] ASC)
)
GO

CREATE TABLE [dbo].[UserRoles](
	[UserRoleId] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[RoleId] [int] NOT NULL,
 CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserRoleId] ASC)
)
GO

ALTER TABLE [dbo].[UserRoles]  WITH CHECK ADD  CONSTRAINT [FK_UserRoles_RoleId] FOREIGN KEY([RoleId])
REFERENCES [dbo].[Roles] ([RoleId])
GO

ALTER TABLE [dbo].[UserRoles] CHECK CONSTRAINT [FK_UserRoles_RoleId]
GO

ALTER TABLE [dbo].[UserRoles]  WITH CHECK ADD  CONSTRAINT [FK_UserRoles_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([UserId])
GO

ALTER TABLE [dbo].[UserRoles] CHECK CONSTRAINT [FK_UserRoles_UserId]
GO

CREATE TABLE [dbo].[RolePermissions](
	[RolePermissionId] [bigint] IDENTITY(1,1) NOT NULL,
	[RoleId] [int] NOT NULL,
	[PermissionKey] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([RolePermissionId] ASC)
)
GO

ALTER TABLE [dbo].[RolePermissions]  WITH CHECK ADD  CONSTRAINT [FK_RolePermissions_RoleId] FOREIGN KEY([RoleId])
REFERENCES [dbo].[Roles] ([RoleId])
GO

ALTER TABLE [dbo].[RolePermissions] CHECK CONSTRAINT [FK_RolePermissions_RoleId]
GO

CREATE TABLE [dbo].[UserPermissions](
	[UserPermissionId] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[PermissionKey] [nvarchar](100) NOT NULL,
	[Granted] [bit] NOT NULL,
 CONSTRAINT [PK_UserPermissions] PRIMARY KEY CLUSTERED ([UserPermissionId] ASC)
)
GO

ALTER TABLE [dbo].[UserPermissions] ADD  CONSTRAINT [DF_UserPermissions_Granted]  DEFAULT ((1)) FOR [Granted]
GO

ALTER TABLE [dbo].[UserPermissions]  WITH CHECK ADD  CONSTRAINT [FK_UserPermissions_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([UserId])
GO

ALTER TABLE [dbo].[UserPermissions] CHECK CONSTRAINT [FK_UserPermissions_UserId]
GO
```

## Client Contract với ApiReponse

Các class C# triển khai mẫu result chung (đề xuất):

```csharp
public class ApiResponse<T>
{
    public T? Data { get; set; }    
    public ApiError? Error { get; init; }

    public static ApiResponse<T> Success(T data) => new() { Data = data };
    public static ApiResponse<T> Failure(ApiError error) => new() { Error = error };
}

public sealed class ApiError
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public IReadOnlyCollection<ApiErrorDetail> Details { get; init; } = [];
}

public sealed class ApiErrorDetail
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public string? Field { get; init; }
}
```

## Kiến trúc kết hợp Mediator + FluentValidation + ErrorOr

### Kiến trúc

Boundary kiến trúc:

```
┌──────────────────────────────────────────┐
│              Application                 │
│                                          │
│ FluentValidation                         │
│       ↓                                  │
│ ValidationBehavior                       │
│       ↓                                  │
│ ErrorOr<T>                               │
│       ↓                                  │
│ Handler                                  │
│       ↓                                  │
│ ErrorOr<T>                               │
└──────────────────┬───────────────────────┘
                   │
                   │ API boundary
                   ▼
┌──────────────────────────────────────────┐
│                  API                     │
│                                          │
│ ErrorOr<T>                               │
│       ↓                                  │
│ ToApiResponse()                          │
│       ↓                                  │
│ ApiResponse<T>                           │
└──────────────────┬───────────────────────┘
                   │
                   ▼
             HTTP / JSON
                   │
                   ▼
┌──────────────────────────────────────────┐
│                 Client                   │
│                                          │
│ ApiResponse<T>                           │
│   ├── data                               │
│   └── error                              │
│        ├── code                          │
│        ├── message                       │
│        └── details[]                     │
└──────────────────────────────────────────┘
```

Flow cụ thể hơn:

```
                    ┌─────────────────┐
                    │ FluentValidator │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Validation      │
                    │ Behavior        │
                    └────────┬────────┘
                             │
                             ▼
                       ErrorOr<T>
                             │
                    ┌────────┴────────┐
                    │                 │
                 Success             Error
                    │                 │
                    └────────┬────────┘
                             ▼
                    ┌─────────────────┐
                    │ ToApiResponse() │  ← API boundary
                    └────────┬────────┘
                             ▼
                    ┌─────────────────┐
                    │ ApiResponse<T>  │
                    └────────┬────────┘
                             ▼
                           JSON
                             │
                             ▼
                          Client
```

**Điểm mấu chốt:** `ValidationBehavior` **không biết HTTP**, Handler **không biết HTTP**, `ErrorOr` **không bị serialize trực tiếp**. Chỉ `ToApiResponse()` ở API boundary biết cách biến `ErrorOr<T>` thành contract duy nhất của Client.

### `ValidationBehavior` đề xuất

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IErrorOr
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v =>
                v.ValidateAsync(context, cancellationToken)));

        var validationErrors = validationResults
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .ToList();

        if (validationErrors.Count == 0)
        {
            return await next();
        }

        var errors = validationErrors
            .Select(error =>
                Error.Validation(
                    code: error.ErrorCode,
                    description: error.ErrorMessage,
                    metadata: new Dictionary<string, object>
                    {
                        ["field"] = ToCamelCase(error.PropertyName)
                    }))
            .ToList();

        return (dynamic)errors;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
```

### FluentValidation Validator, Command, Handler đề xuất:

```csharp
public sealed class CreateUserCommandValidator
    : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("Validation.Required")
            .WithMessage("Name is required.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode("Validation.Required")
            .WithMessage("Email is required.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithErrorCode("Validation.InvalidEmail")
            .WithMessage("Email is invalid.");
    }
}

public sealed record CreateUserCommand(string Name, string Email) : IRequest<ErrorOr<UserResponse>>;

public sealed record UserResponse(Guid Id, string Name, string Email);

public static class UserErrors
{
    public static Error EmailAlreadyExists => Error.Conflict(code: "User.EmailAlreadyExists", description: "Email already exists.");
}

public interface IUserRepository
{
	Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);
}

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, ErrorOr<UserResponse>>
{
    public async Task<ErrorOr<UserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // business logic...
		var exists = await _repository.ExistsByEmailAsync(request.Email, cancellationToken);

		if (exists) return UserErrors.EmailAlreadyExists;
		
        var user = new UserResponse(Guid.NewGuid(), request.Name, request.Email);

        return user;
    }
}
```

### Mapper `ErrorOr` → `ApiError` đề xuất

```csharp
public static class ErrorOrExtensions
{
    public static ApiError ToApiError(
        this List<Error> errors)
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "Errors cannot be empty.",
                nameof(errors));
        }

        var type = errors
            .Select(x => x.Type)
            .Distinct()
            .SingleOrDefault();

        return type switch
        {
            ErrorType.Validation => new ApiError(
                Code: "ValidationError",
                Message: "Validation failed.",
                Details: errors.ToApiErrorDetails()),

            _ => new ApiError(
                Code: errors[0].Code,
                Message: errors[0].Description,
                Details: errors.ToApiErrorDetails())
        };
    }

    private static IReadOnlyCollection<ApiErrorDetail>
        ToApiErrorDetails(
            this IEnumerable<Error> errors)
    {
        return errors
            .Select(error => new ApiErrorDetail(
                Code: error.Code,
                Message: error.Description,
                Field: GetField(error)))
            .ToList();
    }

    private static string? GetField(Error error)
    {
        if (error.Metadata is null)
            return null;

        return error.Metadata.TryGetValue(
            "field",
            out var field)
                ? field?.ToString()
                : null;
    }
}
```

### API phản hồi Client theo constract duy nhất ApiReponse

**Dành cho Mini API**:

```csharp
using ErrorOr;
using MediatR;

public static class ErrorOrToApiReponseExtensions
{
    public static IResult ToApiResponse<T>(
        this ErrorOr<T> result)
    {
        if (result.IsError)
        {
            return result.Errors.ToApiResponse();
        }

        return Results.Ok(
            ApiResponse<T>.Success(result.Value));
    }

    private static IResult ToApiResponse(
        this List<Error> errors)
    {
        var apiError = errors.ToApiError();

        var statusCode = errors
            .Select(x => x.Type)
            .First()
            .ToStatusCode();

        return Results.Json(
            ApiResponse<object>.Failure(apiError),
            statusCode: statusCode);
    }
}

public static class SenderExtensions
{
    public static async Task<IResult> SendApiAsync<TResponse>(
        this ISender sender,
        IRequest<ErrorOr<TResponse>> request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(request, cancellationToken);

        return result.ToApiResponse();
    }
}

// Sử dụng tại endpoint

app.MapPost("/users", async (
    CreateUserCommand command,
    ISender sender,
    CancellationToken ct) =>
{
    return await sender.SendApiAsync(command, ct);
});
```

Cần xây hạ tầng trung để có thể dùng cách tương tự với Desktop.

## Xây dựng Persistence

**Áp dụng CQRS**:
* CURD dùng RepoDb (chỉ thao tác với Repository Interface và Domain.Entities).
* Queries dùng Dapper (không sử dụng Repository interfaces, tạo service phục vụ, ví dụ IProductQueryService) trả về Dto.
* Repositories, UnitOfWork, ...

Code base C# đề xuất:

**Interfaces**:
```csharp
public interface IDbConnectionFactory
{
	IDbConnection Create();
}

public interface IRepositoryFactory
{
	TInterface Create<TInterface>(IDbConnection connection) where TInterface : class;
}

public interface ITransactionProvider
{
	IDbTransaction CurrentTransaction { get; set; }
}

public interface IUnitOfWork : IDisposable
{
	// Hàm lấy nhanh Repository mong muốn trong phiên làm việc hiện tại
	TRepository Repository<TRepository>() where TRepository : class;
	void BeginTransaction();
	void Commit();
	void Rollback();
}

public interface IUnitOfWorkFactory
{
	IUnitOfWork Create();
}
```

**Implements**:
```csharp
public class DbConnectionFactory : IDbConnectionFactory
{
	private readonly string _connectionString;

	public DbConnectionFactory(IConfiguration configuration)
	{
		// Lấy chuỗi kết nối từ file appsettings.json
		_connectionString = configuration.GetConnectionString("DefaultConnection")
			?? throw new ArgumentNullException("Không tìm thấy chuỗi kết nối 'DefaultConnection'.");
	}

	public IDbConnection Create()
	{
		var connection = new SqlConnection(_connectionString);
		
		// Đảm bảo mở sẵn kết nối trước khi trả về theo yêu cầu
		if (connection.State != ConnectionState.Open)
		{
			connection.Open();
		}

		return connection;
	}
}

internal class TransactionProvider : ITransactionProvider
{
	// Giữ nguyên static readonly AsyncLocal theo đúng Best Practice
	private static readonly AsyncLocal<IDbTransaction> _currentTransaction = new AsyncLocal<IDbTransaction>();

	public IDbTransaction CurrentTransaction
	{
		get => _currentTransaction.Value;
		set
		{
			// TRƯỜNG HỢP 1: Gán một Transaction mới vào luồng
			if (value != null)
			{
				// THREAD-SAFE GUARD: Nếu luồng này ĐÃ CÓ một transaction đang chạy, cấm ghi đè!
				if (_currentTransaction.Value != null && _currentTransaction.Value != value)
				{
					throw new InvalidOperationException(
						"LỖI KIẾN TRÚC: Phát hiện hành vi ghi đè Transaction! " +
						"Một giao dịch khác đang được thực thi trên luồng này và chưa được đóng (Commit/Rollback).");
				}

				_currentTransaction.Value = value;
			}

			// TRƯỜNG HỢP 2: Gán bằng null (Hành vi dọn dẹp sau khi Commit/Rollback)
			else
			{
				_currentTransaction.Value = null;
			}
		}
	}
}

internal class UnitOfWork : IUnitOfWork
{
	private readonly IDbConnection _connection;
	private readonly IRepositoryFactory _factory;
	private readonly ITransactionProvider _transactionProvider; // Thêm provider này
	private IDbTransaction _transaction;
	private bool _disposed;

	public UnitOfWork(IDbConnection connection, IRepositoryFactory factory, ITransactionProvider transactionProvider)
	{
		_connection = connection ?? throw new ArgumentNullException(nameof(connection));
		_factory = factory ?? throw new ArgumentNullException(nameof(factory));
		_transactionProvider = transactionProvider ?? throw new ArgumentNullException(nameof(transactionProvider));

		if (_connection.State != ConnectionState.Open)
		{
			_connection.Open();
		}
	}

	public TRepository Repository<TRepository>() where TRepository : class
	{
		return _factory.Create<TRepository>(_connection);
	}

	public void BeginTransaction()
	{
		if (_transaction != null) return;

		_transaction = _connection.BeginTransaction();
		// Đẩy transaction vào luồng lưu trữ tập trung
		_transactionProvider.CurrentTransaction = _transaction;
	}

	public void Commit()
	{
		try
		{
			_transaction?.Commit();
		}
		catch
		{
			Rollback();
			throw;
		}
		finally
		{
			ReleaseTransaction();
		}
	}

	public void Rollback()
	{
		_transaction?.Rollback();
		ReleaseTransaction();
	}

	private void ReleaseTransaction()
	{
		_transaction?.Dispose();
		_transaction = null;
		_transactionProvider.CurrentTransaction = null; // Xóa transaction khỏi bộ nhớ luồng
	}

	public void Dispose()
	{
		if (_disposed) return;
		ReleaseTransaction();
		_connection?.Dispose();
		_disposed = true;
	}
}

internal class UnitOfWorkFactory : IUnitOfWorkFactory
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IDbConnectionFactory _connectionFactory;

	public UnitOfWorkFactory(IServiceProvider serviceProvider, IDbConnectionFactory connectionFactory)
	{
		_serviceProvider = serviceProvider;
		_connectionFactory = connectionFactory;
	}

	public IUnitOfWork Create()
	{
		// 1. Lấy ra kết nối đã được mở sẵn từ Connection Factory
		IDbConnection openedConnection = _connectionFactory.Create();

		// 2. Sử dụng ActivatorUtilities để tự động truyền IDbConnection, 
		// đồng thời DI sẽ tự động tìm kiếm IRepositoryFactory và ITransactionProvider để inject vào UnitOfWork
		return ActivatorUtilities.CreateInstance<UnitOfWork>(_serviceProvider, openedConnection);
	}
}

internal class GenericRepositoryFactory : IRepositoryFactory
{
	private readonly IServiceProvider _serviceProvider;

	// Bộ nhớ đệm dùng chung để lưu vết Map giữa Interface và Class cụ thể
	private static readonly ConcurrentDictionary<Type, Type> _typeCache = new ConcurrentDictionary<Type, Type>();

	public GenericRepositoryFactory(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public TInterface Create<TInterface>(IDbConnection connection) where TInterface : class
	{
		var interfaceType = typeof(TInterface);

		// Lấy từ cache nếu đã từng tìm kiếm, nếu chưa thì mới quét Assembly
		var implementationType = _typeCache.GetOrAdd(interfaceType, @interface =>
		{
			var impl = Assembly.GetExecutingAssembly()
				.GetTypes()
				.FirstOrDefault(t => @interface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

			return impl ?? throw new InvalidOperationException($"Không tìm thấy lớp triển khai {@interface.Name}");
		});

		// Khởi tạo instance và tự động inject các dịch vụ từ DI Container kèm connection
		return (TInterface)ActivatorUtilities.CreateInstance(_serviceProvider, implementationType, connection);
	}
}
```

Ví dụ Repository:
```csharp
namespace MyDmsVn.Server.Domain.Entities
{
	public class Site
	{
		public int RecId { get; set; }
		public string SiteId { get; set; }
		public string SiteName { get; set; }
		public string Address { get; set; }
		public string Phone { get; set; }
		public byte Status { get; set; }
	}
}

namespace MyDmsVn.Server.Domain.Repositories
{
	public interface ISiteRepository
	{
		Task<Site> GetByIdAsync(string siteId, CancellationToken cancellationToken);
		Task<int> CreateAsync(Site site, CancellationToken cancellationToken);
	}
}

internal abstract class BaseRepository
{
	protected readonly IDbConnection Connection;
	private readonly ITransactionProvider _transactionProvider;

	// Hàm tạo nhận connection từ Factory và transactionProvider từ DI
	protected BaseRepository(IDbConnection connection, ITransactionProvider transactionProvider)
	{
		Connection = connection;
		_transactionProvider = transactionProvider;
	}

	// Thuộc tính này tự động lấy transaction hiện tại của Unit of Work (nếu có)
	// Nếu chưa gọi BeginTransaction(), thuộc tính này sẽ trả về null (hợp lệ với Dapper)
	protected IDbTransaction Transaction => _transactionProvider.CurrentTransaction;
}

using RepoDb;	
internal class SiteRepository : BaseRepository, ISiteRepository
{
	public SiteRepository(IDbConnection connection, ITransactionProvider transactionProvider) : base(connection, transactionProvider)
	{
	}

	public async Task<int> CreateAsync(Site site, CancellationToken cancellationToken = default)
	{
		var res = await Connection.InsertAsync(site, cancellationToken: cancellationToken);
		return (int) res;
	}

	public async Task<Site> GetByIdAsync(string siteId, CancellationToken cancellationToken = default)
	{
		return (await Connection.QueryAsync<Site>(e => e.SiteId == siteId, cancellationToken: cancellationToken)).FirstOrDefault();
	}
}
```

Ví dụ sử dụng RepoDb:
```csharp
public interface IEntityMap
{
	void Configure();
}

internal sealed class SiteMap : IEntityMap
{
	public void Configure()
	{
		FluentMapper
			.Entity<Site>()
			.Table("[dbo].[s3_Site]")
			.Primary(e => e.SiteId)
			.Identity(e => e.RecId)
			.Column(e => e.SiteId, "SiteId")
			.Column(e => e.SiteName, "SiteName")
			.Column(e => e.Address, "Address")
			.Column(e => e.Phone, "Phone")
			.Column(e => e.Status, "Status")
			.DbType(e => e.SiteId, DbType.AnsiString)
			.DbType(e => e.SiteName, DbType.AnsiString)
			.DbType(e => e.Address, DbType.AnsiString)
			.DbType(e => e.Phone, DbType.AnsiString)
			.DbType(e => e.Status, DbType.Byte)
			.PropertyValueAttributes(e => e.SiteId, [new SizeAttribute(50)])
			.PropertyValueAttributes(e => e.SiteName, [new SizeAttribute(255)])
			.PropertyValueAttributes(e => e.Address, [new SizeAttribute(255)])
			.PropertyValueAttributes(e => e.Phone, [new SizeAttribute(100)])
			;
	}
}

internal static class RepoDbBootstrapper
{
	private static int _initialized;
	internal static void EnsureInitialized()
	{
		if (Interlocked.Exchange(ref _initialized, 1) == 1)
		{
			return;
		}

		GlobalConfiguration.Setup().UseSqlServer();
		
		// Quét và thu thập IEntityMap
		var mapTypes = typeof(RepoDbBootstrapper).Assembly.GetTypes()
			.Where(t => typeof(IEntityMap).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

		foreach (var type in mapTypes)
		{
			var mapper = (IEntityMap)Activator.CreateInstance(type);
			mapper.Configure();
		}
	}
}
```

## WinForms UI
Sử dụng các submodule sau để xây dựng UI:
- [MyDmsVn.Bootstrap5WinFormUI](https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI)
- [MyDmsVn.BootstrapSourceGrid](https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid)
