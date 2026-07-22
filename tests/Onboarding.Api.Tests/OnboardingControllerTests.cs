using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Onboarding.Api.Controllers;
using Onboarding.Api.Data;
using Onboarding.Api.Entities;
using Onboarding.Api.Models;
using Onboarding.Api.Services;
using Microsoft.Data.Sqlite;

namespace Onboarding.Api.Tests;

[TestFixture]
public sealed class OnboardingControllerTests
{
   private SqliteConnection _connection = null!;
private OnboardingDbContext _dbContext = null!;
private OnboardingController _controller = null!;

    private TestAiFailureExplanationService
        _aiFailureExplanationService = null!;

    [SetUp]
public void SetUp()
{
    _connection =
        new SqliteConnection(
            "Data Source=:memory:");

    _connection.Open();

    var options =
        new DbContextOptionsBuilder<OnboardingDbContext>()
            .UseSqlite(_connection)
            .Options;

    _dbContext =
        new OnboardingDbContext(options);

    _dbContext.Database.EnsureCreated();

    var httpClient =
        new HttpClient(
            new TestHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        System.Net.HttpStatusCode.OK)))
        {
            BaseAddress =
                new Uri("http://localhost")
        };

    var httpClientFactory =
        new TestHttpClientFactory(httpClient);

    _aiFailureExplanationService =
        new TestAiFailureExplanationService();

    _controller =
        new OnboardingController(
            _dbContext,
            httpClientFactory,
            NullLogger<OnboardingController>.Instance,
            _aiFailureExplanationService);
}

    [TearDown]
public async Task TearDown()
{
    await _dbContext.Database
        .EnsureDeletedAsync();

    await _dbContext.DisposeAsync();
    await _connection.DisposeAsync();
}

    [Test]
    public async Task Create_WithValidRequest_ReturnsCreatedResponse()
    {
        var request =
            CreateValidRequest("EMP-TEST-1001");

        var result =
            await _controller.Create(
                request,
                CancellationToken.None);

        var createdResult =
            result.Result as CreatedAtActionResult;

        Assert.That(
            createdResult,
            Is.Not.Null);

        Assert.That(
            createdResult!.StatusCode,
            Is.EqualTo(201));

        var response =
            createdResult.Value as OnboardingResponse;

        Assert.That(
            response,
            Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                response!.EmployeeNumber,
                Is.EqualTo("EMP-TEST-1001"));

            Assert.That(
                response.EmployeeName,
                Is.EqualTo("Demo User"));

            Assert.That(
                response.Status,
                Is.EqualTo("Pending"));

            Assert.That(
                response.RetryCount,
                Is.EqualTo(0));

            Assert.That(
                response.IsRetryable,
                Is.False);

            Assert.That(
                response.TransactionId,
                Is.Not.EqualTo(Guid.Empty));
        });

        var savedTransaction =
            await _dbContext.OnboardingTransactions
                .SingleAsync();

        Assert.That(
            savedTransaction.EmployeeNumber,
            Is.EqualTo("EMP-TEST-1001"));
    }

    [Test]
    public async Task Create_NormalizesEmployeeNumberAndEmail()
    {
        var request =
            CreateValidRequest(
                "  emp-test-1002  ");

        request.Email =
            "  DEMO.USER@EXAMPLE.COM  ";

        var result =
            await _controller.Create(
                request,
                CancellationToken.None);

        var createdResult =
            result.Result as CreatedAtActionResult;

        Assert.That(
            createdResult,
            Is.Not.Null);

        var savedTransaction =
            await _dbContext.OnboardingTransactions
                .SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                savedTransaction.EmployeeNumber,
                Is.EqualTo("EMP-TEST-1002"));

            Assert.That(
                savedTransaction.Email,
                Is.EqualTo("demo.user@example.com"));
        });
    }

    [Test]
    public async Task Create_WithDuplicateEmployeeNumber_ReturnsConflict()
    {
        var existingTransaction =
            CreateTransaction("EMP-TEST-2001");

        _dbContext.OnboardingTransactions.Add(
            existingTransaction);

        await _dbContext.SaveChangesAsync();

        var request =
            CreateValidRequest("emp-test-2001");

        var result =
            await _controller.Create(
                request,
                CancellationToken.None);

        var conflictResult =
            result.Result as ConflictObjectResult;

        Assert.That(
            conflictResult,
            Is.Not.Null);

        Assert.That(
            conflictResult!.StatusCode,
            Is.EqualTo(409));

        var transactionCount =
            await _dbContext.OnboardingTransactions
                .CountAsync();

        Assert.That(
            transactionCount,
            Is.EqualTo(1));
    }

    [Test]
    public async Task GetByTransactionId_WithExistingTransaction_ReturnsOk()
    {
        var transaction =
            CreateTransaction("EMP-TEST-3001");

        _dbContext.OnboardingTransactions.Add(
            transaction);

        await _dbContext.SaveChangesAsync();

        var result =
            await _controller.GetByTransactionId(
                transaction.TransactionId,
                CancellationToken.None);

        var okResult =
            result.Result as OkObjectResult;

        Assert.That(
            okResult,
            Is.Not.Null);

        var response =
            okResult!.Value as OnboardingResponse;

        Assert.That(
            response,
            Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                response!.TransactionId,
                Is.EqualTo(
                    transaction.TransactionId));

            Assert.That(
                response.EmployeeNumber,
                Is.EqualTo("EMP-TEST-3001"));

            Assert.That(
                response.Status,
                Is.EqualTo("Pending"));
        });
    }

    [Test]
    public async Task GetByTransactionId_WithUnknownTransaction_ReturnsNotFound()
    {
        var unknownTransactionId =
            Guid.NewGuid();

        var result =
            await _controller.GetByTransactionId(
                unknownTransactionId,
                CancellationToken.None);

        var notFoundResult =
            result.Result as NotFoundObjectResult;

        Assert.That(
            notFoundResult,
            Is.Not.Null);

        Assert.That(
            notFoundResult!.StatusCode,
            Is.EqualTo(404));
    }

    [Test]
public async Task GetAll_ReturnsTransactionsNewestFirst()
{
    var olderTransaction =
        CreateTransaction("EMP-TEST-4001");

    olderTransaction.CreatedAtUtc =
        DateTime.UtcNow.AddMinutes(-10);

    var newerTransaction =
        CreateTransaction("EMP-TEST-4002");

    newerTransaction.CreatedAtUtc =
        DateTime.UtcNow;

    _dbContext.OnboardingTransactions.AddRange(
        olderTransaction,
        newerTransaction);

    await _dbContext.SaveChangesAsync();

    var parameters =
        new OnboardingQueryParameters
        {
            PageNumber = 1,
            PageSize = 10
        };

    var result =
        await _controller.GetAll(
            parameters,
            CancellationToken.None);

    var okResult =
        result.Result as OkObjectResult;

    Assert.That(
        okResult,
        Is.Not.Null);

    var response =
        okResult!.Value as
            PagedResponse<OnboardingResponse>;

    Assert.That(
        response,
        Is.Not.Null);

    var responseList =
        response!.Items.ToList();

    Assert.Multiple(() =>
    {
        Assert.That(
            responseList,
            Has.Count.EqualTo(2));

        Assert.That(
            responseList[0].EmployeeNumber,
            Is.EqualTo("EMP-TEST-4002"));

        Assert.That(
            responseList[1].EmployeeNumber,
            Is.EqualTo("EMP-TEST-4001"));

        Assert.That(
            response.TotalItems,
            Is.EqualTo(2));

        Assert.That(
            response.TotalPages,
            Is.EqualTo(1));

        Assert.That(
            response.PageNumber,
            Is.EqualTo(1));

        Assert.That(
            response.PageSize,
            Is.EqualTo(10));

        Assert.That(
            response.HasPreviousPage,
            Is.False);

        Assert.That(
            response.HasNextPage,
            Is.False);
    });
}

   [Test]
public async Task GetAll_WithPagination_ReturnsRequestedPage()
{
    for (var index = 1; index <= 5; index++)
    {
        var transaction =
            CreateTransaction(
                $"EMP-PAGE-{index:0000}");

        transaction.CreatedAtUtc =
            DateTime.UtcNow.AddMinutes(-index);

        _dbContext.OnboardingTransactions.Add(
            transaction);
    }

    await _dbContext.SaveChangesAsync();

    var parameters =
        new OnboardingQueryParameters
        {
            PageNumber = 2,
            PageSize = 2
        };

    var result =
        await _controller.GetAll(
            parameters,
            CancellationToken.None);

    var okResult =
        result.Result as OkObjectResult;

    var response =
        okResult!.Value as
            PagedResponse<OnboardingResponse>;

    Assert.That(
        response,
        Is.Not.Null);

    Assert.Multiple(() =>
    {
        Assert.That(
            response!.Items,
            Has.Count.EqualTo(2));

        Assert.That(
            response.PageNumber,
            Is.EqualTo(2));

        Assert.That(
            response.PageSize,
            Is.EqualTo(2));

        Assert.That(
            response.TotalItems,
            Is.EqualTo(5));

        Assert.That(
            response.TotalPages,
            Is.EqualTo(3));

        Assert.That(
            response.HasPreviousPage,
            Is.True);

        Assert.That(
            response.HasNextPage,
            Is.True);
    });
}

    [Test]
public async Task GetAll_WithStatusFilter_ReturnsMatchingTransactions()
{
    var completedTransaction =
        CreateTransaction(
            "EMP-FILTER-1001");

    completedTransaction.Status =
        "Completed";

    completedTransaction.HcmEmployeeId =
        "HCM-FILTER-1001";

    var failedTransaction =
        CreateTransaction(
            "EMP-FILTER-1002");

    failedTransaction.Status =
        "ValidationFailed";

    failedTransaction.ErrorCode =
        "HCM-VALIDATION-400";

    _dbContext.OnboardingTransactions.AddRange(
        completedTransaction,
        failedTransaction);

    await _dbContext.SaveChangesAsync();

    var parameters =
        new OnboardingQueryParameters
        {
            Status = "Completed",
            PageNumber = 1,
            PageSize = 10
        };

    var result =
        await _controller.GetAll(
            parameters,
            CancellationToken.None);

    var okResult =
        result.Result as OkObjectResult;

    var response =
        okResult!.Value as
            PagedResponse<OnboardingResponse>;

    Assert.That(
        response,
        Is.Not.Null);

    Assert.Multiple(() =>
    {
        Assert.That(
            response!.Items,
            Has.Count.EqualTo(1));

        Assert.That(
            response.Items.Single().EmployeeNumber,
            Is.EqualTo("EMP-FILTER-1001"));

        Assert.That(
            response.TotalItems,
            Is.EqualTo(1));
    });
}
 [Test]
public async Task GetAll_WithSearch_ReturnsMatchingEmployee()
{
    var firstTransaction =
        CreateTransaction(
            "EMP-SEARCH-1001");

    firstTransaction.FirstName =
        "Alpha";

    firstTransaction.LastName =
        "Engineer";

    var secondTransaction =
        CreateTransaction(
            "EMP-SEARCH-1002");

    secondTransaction.FirstName =
        "Beta";

    secondTransaction.LastName =
        "Analyst";

    _dbContext.OnboardingTransactions.AddRange(
        firstTransaction,
        secondTransaction);

    await _dbContext.SaveChangesAsync();

    var parameters =
        new OnboardingQueryParameters
        {
            Search = "alpha",
            PageNumber = 1,
            PageSize = 10
        };

    var result =
        await _controller.GetAll(
            parameters,
            CancellationToken.None);

    var okResult =
        result.Result as OkObjectResult;

    var response =
        okResult!.Value as
            PagedResponse<OnboardingResponse>;

    Assert.That(
        response,
        Is.Not.Null);

    Assert.Multiple(() =>
    {
        Assert.That(
            response!.Items,
            Has.Count.EqualTo(1));

        Assert.That(
            response.Items.Single().EmployeeNumber,
            Is.EqualTo("EMP-SEARCH-1001"));

        Assert.That(
            response.TotalItems,
            Is.EqualTo(1));
    });
}



    [Test]
    public async Task Process_WithCompletedTransaction_ReturnsConflict()
    {
        var transaction =
            CreateTransaction("EMP-TEST-5001");

        transaction.Status =
            "Completed";

        transaction.HcmEmployeeId =
            "HCM-TEST-5001";

        _dbContext.OnboardingTransactions.Add(
            transaction);

        await _dbContext.SaveChangesAsync();

        var result =
            await _controller.Process(
                transaction.TransactionId,
                CancellationToken.None);

        var conflictResult =
            result.Result as ConflictObjectResult;

        Assert.That(
            conflictResult,
            Is.Not.Null);

        Assert.That(
            conflictResult!.StatusCode,
            Is.EqualTo(409));
    }

    [Test]
    public async Task Retry_WithNonRetryableTransaction_ReturnsConflict()
    {
        var transaction =
            CreateTransaction("EMP-TEST-6001");

        transaction.Status =
            "ValidationFailed";

        transaction.ErrorCode =
            "HCM-VALIDATION-400";

        transaction.IsRetryable =
            false;

        transaction.RetryCount =
            1;

        _dbContext.OnboardingTransactions.Add(
            transaction);

        await _dbContext.SaveChangesAsync();

        var result =
            await _controller.Retry(
                transaction.TransactionId,
                CancellationToken.None);

        var conflictResult =
            result.Result as ConflictObjectResult;

        Assert.That(
            conflictResult,
            Is.Not.Null);

        Assert.That(
            conflictResult!.StatusCode,
            Is.EqualTo(409));
    }

    [Test]
    public async Task Retry_WhenMaximumAttemptsReached_UpdatesStatus()
    {
        var transaction =
            CreateTransaction("EMP-TEST-7001");

        transaction.Status =
            "TimedOut";

        transaction.ErrorCode =
            "HCM-TIMEOUT-408";

        transaction.IsRetryable =
            true;

        transaction.RetryCount =
            3;

        _dbContext.OnboardingTransactions.Add(
            transaction);

        await _dbContext.SaveChangesAsync();

        var result =
            await _controller.Retry(
                transaction.TransactionId,
                CancellationToken.None);

        var conflictResult =
            result.Result as ConflictObjectResult;

        Assert.That(
            conflictResult,
            Is.Not.Null);

        Assert.That(
            conflictResult!.StatusCode,
            Is.EqualTo(409));

        var updatedTransaction =
            await _dbContext.OnboardingTransactions
                .SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                updatedTransaction.Status,
                Is.EqualTo("RetryLimitExceeded"));

            Assert.That(
                updatedTransaction.IsRetryable,
                Is.False);

            Assert.That(
                updatedTransaction.RetryCount,
                Is.EqualTo(3));

            Assert.That(
                updatedTransaction.UpdatedAtUtc,
                Is.Not.Null);
        });
    }
    
    [Test]
public async Task GetSummary_ReturnsCorrectTransactionCounts()
{
    var pendingTransaction =
        CreateTransaction(
            "EMP-SUMMARY-1001");

    pendingTransaction.Status =
        "Pending";

    var completedTransaction =
        CreateTransaction(
            "EMP-SUMMARY-1002");

    completedTransaction.Status =
        "Completed";

    completedTransaction.HcmEmployeeId =
        "HCM-SUMMARY-1002";

    var retryableTransaction =
        CreateTransaction(
            "EMP-SUMMARY-1003");

    retryableTransaction.Status =
        "TemporaryFailure";

    retryableTransaction.ErrorCode =
        "HCM-UNAVAILABLE-503";

    retryableTransaction.ErrorMessage =
        "The downstream service is temporarily unavailable.";

    retryableTransaction.IsRetryable =
        true;

    retryableTransaction.RetryCount =
        1;

    var validationFailedTransaction =
        CreateTransaction(
            "EMP-SUMMARY-1004");

    validationFailedTransaction.Status =
        "ValidationFailed";

    validationFailedTransaction.ErrorCode =
        "HCM-VALIDATION-400";

    validationFailedTransaction.ErrorMessage =
        "The downstream system rejected the transaction.";

    validationFailedTransaction.IsRetryable =
        false;

    validationFailedTransaction.RetryCount =
        1;

    var retryLimitTransaction =
        CreateTransaction(
            "EMP-SUMMARY-1005");

    retryLimitTransaction.Status =
        "RetryLimitExceeded";

    retryLimitTransaction.ErrorCode =
        "HCM-TIMEOUT-408";

    retryLimitTransaction.ErrorMessage =
        "The downstream request timed out.";

    retryLimitTransaction.IsRetryable =
        false;

    retryLimitTransaction.RetryCount =
        3;

    _dbContext.OnboardingTransactions.AddRange(
        pendingTransaction,
        completedTransaction,
        retryableTransaction,
        validationFailedTransaction,
        retryLimitTransaction);

    await _dbContext.SaveChangesAsync();

    var result =
        await _controller.GetSummary(
            CancellationToken.None);

    var okResult =
        result.Result as OkObjectResult;

    Assert.That(
        okResult,
        Is.Not.Null);

    var response =
        okResult!.Value as
            OnboardingSummaryResponse;

    Assert.That(
        response,
        Is.Not.Null);

    Assert.Multiple(() =>
    {
        Assert.That(
            okResult.StatusCode,
            Is.EqualTo(200));

        Assert.That(
            response!.TotalTransactions,
            Is.EqualTo(5));

        Assert.That(
            response.PendingTransactions,
            Is.EqualTo(1));

        Assert.That(
            response.ProcessingTransactions,
            Is.EqualTo(0));

        Assert.That(
            response.CompletedTransactions,
            Is.EqualTo(1));

        Assert.That(
            response.FailedTransactions,
            Is.EqualTo(3));

        Assert.That(
            response.RetryableFailures,
            Is.EqualTo(1));

        Assert.That(
            response.RetryLimitExceededTransactions,
            Is.EqualTo(1));

        Assert.That(
            response.GeneratedAtUtc,
            Is.Not.EqualTo(default(DateTime)));
    });
}

    [Test]
    public async Task ExplainFailure_WithUnknownTransaction_ReturnsNotFound()
    {
        var transactionId =
            Guid.NewGuid();

        var result =
            await _controller.ExplainFailure(
                transactionId,
                CancellationToken.None);

        var notFoundResult =
            result.Result as NotFoundObjectResult;

        Assert.That(
            notFoundResult,
            Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                notFoundResult!.StatusCode,
                Is.EqualTo(404));

            Assert.That(
                _aiFailureExplanationService.CallCount,
                Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ExplainFailure_WithPendingTransaction_ReturnsConflict()
    {
        var transaction =
            CreateTransaction(
                "EMP-AI-ENDPOINT-1001");

        transaction.Status =
            "Pending";

        _dbContext.OnboardingTransactions.Add(
            transaction);

        await _dbContext.SaveChangesAsync();

        var result =
            await _controller.ExplainFailure(
                transaction.TransactionId,
                CancellationToken.None);

        var conflictResult =
            result.Result as ConflictObjectResult;

        Assert.That(
            conflictResult,
            Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                conflictResult!.StatusCode,
                Is.EqualTo(409));

            Assert.That(
                _aiFailureExplanationService.CallCount,
                Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ExplainFailure_WithCompletedTransaction_ReturnsConflict()
    {
        var transaction =
            CreateTransaction(
                "EMP-AI-ENDPOINT-1002");

        transaction.Status =
            "Completed";

        transaction.HcmEmployeeId =
            "HCM-AI-ENDPOINT-1002";

        _dbContext.OnboardingTransactions.Add(
            transaction);

        await _dbContext.SaveChangesAsync();

        var result =
            await _controller.ExplainFailure(
                transaction.TransactionId,
                CancellationToken.None);

        var conflictResult =
            result.Result as ConflictObjectResult;

        Assert.That(
            conflictResult,
            Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                conflictResult!.StatusCode,
                Is.EqualTo(409));

            Assert.That(
                _aiFailureExplanationService.CallCount,
                Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ExplainFailure_WithFailedTransaction_ReturnsExplanation()
    {
        var transaction =
            CreateTransaction(
                "EMP-AI-ENDPOINT-1003");

        transaction.Status =
            "ValidationFailed";

        transaction.ErrorCode =
            "HCM-VALIDATION-400";

        transaction.ErrorMessage =
            "The simulated HCM system rejected the employee data.";

        transaction.IsRetryable =
            false;

        transaction.RetryCount =
            1;

        _dbContext.OnboardingTransactions.Add(
            transaction);

        await _dbContext.SaveChangesAsync();

        var result =
            await _controller.ExplainFailure(
                transaction.TransactionId,
                CancellationToken.None);

        var okResult =
            result.Result as OkObjectResult;

        Assert.That(
            okResult,
            Is.Not.Null);

        var response =
            okResult!.Value as
                AiFailureExplanationResponse;

        Assert.That(
            response,
            Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                okResult.StatusCode,
                Is.EqualTo(200));

            Assert.That(
                response!.TransactionId,
                Is.EqualTo(
                    transaction.TransactionId));

            Assert.That(
                response.Status,
                Is.EqualTo("ValidationFailed"));

            Assert.That(
                response.ErrorCode,
                Is.EqualTo("HCM-VALIDATION-400"));

            Assert.That(
                response.IsRetryable,
                Is.False);

            Assert.That(
                response.RetryCount,
                Is.EqualTo(1));

            Assert.That(
                response.Explanation,
                Does.Contain("Known facts:"));

            Assert.That(
                response.Model,
                Is.EqualTo("test-approved-model"));

            Assert.That(
                _aiFailureExplanationService.CallCount,
                Is.EqualTo(1));
        });
    }

    private static OnboardingRequest CreateValidRequest(
        string employeeNumber)
    {
        return new OnboardingRequest
        {
            EmployeeNumber =
                employeeNumber,

            FirstName =
                "Demo",

            LastName =
                "User",

            Email =
                "demo.user@example.com",

            Department =
                "Engineering",

            Country =
                "India",

            JoiningDate =
                new DateOnly(2026, 8, 17)
        };
    }

    private static OnboardingTransaction CreateTransaction(
        string employeeNumber)
    {
        return new OnboardingTransaction
        {
            TransactionId =
                Guid.NewGuid(),

            EmployeeNumber =
                employeeNumber,

            FirstName =
                "Demo",

            LastName =
                "User",

            Email =
                "demo.user@example.com",

            Department =
                "Engineering",

            Country =
                "India",

            JoiningDate =
                new DateOnly(2026, 8, 17),

            Status =
                "Pending",

            HcmEmployeeId =
                null,

            ErrorCode =
                null,

            ErrorMessage =
                null,

            IsRetryable =
                false,

            RetryCount =
                0,

            CreatedAtUtc =
                DateTime.UtcNow,

            UpdatedAtUtc =
                null,

            LastAttemptAtUtc =
                null
        };
    }

    private sealed class TestHttpClientFactory(
        HttpClient httpClient)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            return httpClient;
        }
    }

    private sealed class TestHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage>
            responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            var response =
                responseFactory(request);

            return Task.FromResult(response);
        }
    }

    private sealed class TestAiFailureExplanationService
        : IAiFailureExplanationService
    {
        public int CallCount { get; private set; }

        public Task<AiFailureExplanationResponse>
            ExplainAsync(
                OnboardingTransaction transaction,
                CancellationToken cancellationToken)
        {
            CallCount++;

            var response =
                new AiFailureExplanationResponse
                {
                    TransactionId =
                        transaction.TransactionId,

                    Status =
                        transaction.Status,

                    ErrorCode =
                        transaction.ErrorCode,

                    IsRetryable =
                        transaction.IsRetryable,

                    RetryCount =
                        transaction.RetryCount,

                    Explanation =
                        "Known facts: The transaction failed. " +
                        "Probable explanation: The recorded error identifies a processing failure. " +
                        "Recommended action: Review the approved operational guidance. " +
                        "Retry guidance: Follow the recorded retry eligibility.",

                    Model =
                        "test-approved-model",

                    GeneratedAtUtc =
                        DateTime.UtcNow
                };

            return Task.FromResult(response);
        }
    }
}