using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Onboarding.Api.Controllers;
using Onboarding.Api.Data;
using Onboarding.Api.Entities;
using Onboarding.Api.Models;

namespace Onboarding.Api.Tests;

[TestFixture]
public sealed class OnboardingControllerTests
{
    private OnboardingDbContext _dbContext = null!;
    private OnboardingController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        var databaseName =
            $"OnboardingTests-{Guid.NewGuid()}";

        var options =
            new DbContextOptionsBuilder<OnboardingDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

        _dbContext =
            new OnboardingDbContext(options);

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

        _controller =
            new OnboardingController(
                _dbContext,
                httpClientFactory,
                NullLogger<OnboardingController>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database
            .EnsureDeletedAsync();

        await _dbContext.DisposeAsync();
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
                Is.EqualTo(transaction.TransactionId));

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

        var result =
            await _controller.GetAll(
                CancellationToken.None);

        var okResult =
            result.Result as OkObjectResult;

        Assert.That(
            okResult,
            Is.Not.Null);

        var responses =
            okResult!.Value as
                IReadOnlyCollection<OnboardingResponse>;

        Assert.That(
            responses,
            Is.Not.Null);

        var responseList =
            responses!.ToList();

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

    private static OnboardingRequest CreateValidRequest(
        string employeeNumber)
    {
        return new OnboardingRequest
        {
            EmployeeNumber = employeeNumber,
            FirstName = "Demo",
            LastName = "User",
            Email = "demo.user@example.com",
            Department = "Engineering",
            Country = "India",
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
}