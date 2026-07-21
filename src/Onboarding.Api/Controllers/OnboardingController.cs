using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Onboarding.Api.Data;
using Onboarding.Api.Entities;
using Onboarding.Api.Models;

namespace Onboarding.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(
    OnboardingDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<OnboardingController> logger) : ControllerBase
{
    private const int MaximumProcessingAttempts = 3;

    [HttpPost]
    [ProducesResponseType(
        typeof(OnboardingResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OnboardingResponse>> Create(
        [FromBody] OnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var employeeNumber = request.EmployeeNumber
            .Trim()
            .ToUpperInvariant();

        var alreadyExists =
            await dbContext.OnboardingTransactions
                .AsNoTracking()
                .AnyAsync(
                    transaction =>
                        transaction.EmployeeNumber ==
                        employeeNumber,
                    cancellationToken);

        if (alreadyExists)
        {
            logger.LogWarning(
                "Duplicate onboarding request rejected for employee {EmployeeNumber}",
                employeeNumber);

            return Conflict(new
            {
                message =
                    $"An onboarding transaction already exists for employee number '{employeeNumber}'."
            });
        }

        var transaction = new OnboardingTransaction
        {
            TransactionId = Guid.NewGuid(),
            EmployeeNumber = employeeNumber,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email
                .Trim()
                .ToLowerInvariant(),
            Department = request.Department.Trim(),
            Country = request.Country.Trim(),
            JoiningDate = request.JoiningDate,
            Status = "Pending",
            HcmEmployeeId = null,
            ErrorCode = null,
            ErrorMessage = null,
            IsRetryable = false,
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = null,
            LastAttemptAtUtc = null
        };

        dbContext.OnboardingTransactions.Add(transaction);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);

            logger.LogInformation(
                "Onboarding transaction {TransactionId} created for employee {EmployeeNumber} with status {Status}",
                transaction.TransactionId,
                transaction.EmployeeNumber,
                transaction.Status);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(
                exception,
                "Duplicate onboarding request rejected while saving employee {EmployeeNumber}",
                employeeNumber);

            return Conflict(new
            {
                message =
                    $"An onboarding transaction already exists for employee number '{employeeNumber}'."
            });
        }

        return CreatedAtAction(
            nameof(GetByTransactionId),
            new
            {
                transactionId =
                    transaction.TransactionId
            },
            MapToResponse(transaction));
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyCollection<OnboardingResponse>),
        StatusCodes.Status200OK)]
    public async Task<
        ActionResult<IReadOnlyCollection<OnboardingResponse>>>
        GetAll(CancellationToken cancellationToken)
    {
        var transactions =
            await dbContext.OnboardingTransactions
                .AsNoTracking()
                .OrderByDescending(
                    transaction =>
                        transaction.CreatedAtUtc)
                .Select(transaction =>
                    new OnboardingResponse
                    {
                        TransactionId =
                            transaction.TransactionId,

                        EmployeeNumber =
                            transaction.EmployeeNumber,

                        EmployeeName =
                            transaction.FirstName +
                            " " +
                            transaction.LastName,

                        Status =
                            transaction.Status,

                        HcmEmployeeId =
                            transaction.HcmEmployeeId,

                        ErrorCode =
                            transaction.ErrorCode,

                        ErrorMessage =
                            transaction.ErrorMessage,

                        IsRetryable =
                            transaction.IsRetryable,

                        RetryCount =
                            transaction.RetryCount,

                        CreatedAtUtc =
                            transaction.CreatedAtUtc,

                        UpdatedAtUtc =
                            transaction.UpdatedAtUtc,

                        LastAttemptAtUtc =
                            transaction.LastAttemptAtUtc
                    })
                .ToListAsync(cancellationToken);

        return Ok(transactions);
    }

    [HttpGet("{transactionId:guid}")]
    [ProducesResponseType(
        typeof(OnboardingResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnboardingResponse>>
        GetByTransactionId(
            Guid transactionId,
            CancellationToken cancellationToken)
    {
        var transaction =
            await dbContext.OnboardingTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item =>
                        item.TransactionId ==
                        transactionId,
                    cancellationToken);

        if (transaction is null)
        {
            logger.LogWarning(
                "Onboarding transaction {TransactionId} was not found",
                transactionId);

            return NotFound(new
            {
                message =
                    $"Transaction '{transactionId}' was not found."
            });
        }

        return Ok(MapToResponse(transaction));
    }

    [HttpPost("{transactionId:guid}/process")]
    [ProducesResponseType(
        typeof(OnboardingResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OnboardingResponse>> Process(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var transaction =
            await dbContext.OnboardingTransactions
                .FirstOrDefaultAsync(
                    item =>
                        item.TransactionId ==
                        transactionId,
                    cancellationToken);

        if (transaction is null)
        {
            logger.LogWarning(
                "Processing rejected because transaction {TransactionId} was not found",
                transactionId);

            return NotFound(new
            {
                message =
                    $"Transaction '{transactionId}' was not found."
            });
        }

        if (!string.Equals(
                transaction.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Initial processing rejected for transaction {TransactionId}, employee {EmployeeNumber}, current status {Status}",
                transaction.TransactionId,
                transaction.EmployeeNumber,
                transaction.Status);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' cannot be processed because its current status is '{transaction.Status}'. Use the retry endpoint for retryable failures."
            });
        }

        logger.LogInformation(
            "Initial processing requested for transaction {TransactionId}, employee {EmployeeNumber}",
            transaction.TransactionId,
            transaction.EmployeeNumber);

        return await ProcessTransactionAsync(
            transaction,
            cancellationToken);
    }

    [HttpPost("{transactionId:guid}/retry")]
    [ProducesResponseType(
        typeof(OnboardingResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OnboardingResponse>> Retry(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var transaction =
            await dbContext.OnboardingTransactions
                .FirstOrDefaultAsync(
                    item =>
                        item.TransactionId ==
                        transactionId,
                    cancellationToken);

        if (transaction is null)
        {
            logger.LogWarning(
                "Retry rejected because transaction {TransactionId} was not found",
                transactionId);

            return NotFound(new
            {
                message =
                    $"Transaction '{transactionId}' was not found."
            });
        }

        if (string.Equals(
                transaction.Status,
                "Completed",
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Retry rejected for completed transaction {TransactionId}, employee {EmployeeNumber}",
                transaction.TransactionId,
                transaction.EmployeeNumber);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' has already been completed and cannot be retried."
            });
        }

        if (!transaction.IsRetryable)
        {
            logger.LogWarning(
                "Retry rejected for transaction {TransactionId}, employee {EmployeeNumber}, status {Status}, error code {ErrorCode}",
                transaction.TransactionId,
                transaction.EmployeeNumber,
                transaction.Status,
                transaction.ErrorCode);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' is not eligible for retry because its failure is non-retryable."
            });
        }

        if (transaction.RetryCount >=
            MaximumProcessingAttempts)
        {
            transaction.Status =
                "RetryLimitExceeded";

            transaction.IsRetryable = false;
            transaction.UpdatedAtUtc =
                DateTime.UtcNow;

            await dbContext.SaveChangesAsync(
                cancellationToken);

            logger.LogWarning(
                "Retry limit reached for transaction {TransactionId}, employee {EmployeeNumber}, retry count {RetryCount}",
                transaction.TransactionId,
                transaction.EmployeeNumber,
                transaction.RetryCount);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' has reached the maximum limit of {MaximumProcessingAttempts} processing attempts."
            });
        }

        logger.LogInformation(
            "Retry requested for transaction {TransactionId}, employee {EmployeeNumber}, current attempt count {RetryCount}",
            transaction.TransactionId,
            transaction.EmployeeNumber,
            transaction.RetryCount);

        return await ProcessTransactionAsync(
            transaction,
            cancellationToken);
    }

    private async Task<ActionResult<OnboardingResponse>>
        ProcessTransactionAsync(
            OnboardingTransaction transaction,
            CancellationToken cancellationToken)
    {
        transaction.Status = "Processing";
        transaction.RetryCount++;
        transaction.LastAttemptAtUtc =
            DateTime.UtcNow;
        transaction.UpdatedAtUtc =
            DateTime.UtcNow;
        transaction.ErrorCode = null;
        transaction.ErrorMessage = null;
        transaction.IsRetryable = false;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        logger.LogInformation(
            "Processing started for transaction {TransactionId}, employee {EmployeeNumber}, attempt {RetryCount}",
            transaction.TransactionId,
            transaction.EmployeeNumber,
            transaction.RetryCount);

        var hcmRequest =
            new HcmCreateEmployeeRequest
            {
                EmployeeNumber =
                    transaction.EmployeeNumber,

                FirstName =
                    transaction.FirstName,

                LastName =
                    transaction.LastName,

                Email =
                    transaction.Email,

                Department =
                    transaction.Department,

                Country =
                    transaction.Country,

                JoiningDate =
                    transaction.JoiningDate
            };

        var httpClient =
            httpClientFactory.CreateClient(
                "MockHcmApi");

        try
        {
            using var hcmResponse =
                await httpClient.PostAsJsonAsync(
                    "/api/employees",
                    hcmRequest,
                    cancellationToken);

            if (hcmResponse.IsSuccessStatusCode)
            {
                var successResponse =
                    await hcmResponse.Content
                        .ReadFromJsonAsync<
                            HcmCreateEmployeeResponse>(
                            cancellationToken:
                                cancellationToken);

                if (successResponse is null)
                {
                    await UpdateFailureAsync(
                        transaction,
                        status: "Failed",
                        errorCode:
                            "HCM-INVALID-RESPONSE",
                        errorMessage:
                            "The simulated HCM API returned an empty success response.",
                        isRetryable: true,
                        cancellationToken);

                    return Ok(
                        MapToResponse(transaction));
                }

                transaction.Status = "Completed";

                transaction.HcmEmployeeId =
                    successResponse.HcmEmployeeId;

                transaction.ErrorCode = null;
                transaction.ErrorMessage = null;
                transaction.IsRetryable = false;

                transaction.UpdatedAtUtc =
                    DateTime.UtcNow;

                await dbContext.SaveChangesAsync(
                    cancellationToken);

                logger.LogInformation(
                    "Transaction {TransactionId} completed for employee {EmployeeNumber} with HCM employee ID {HcmEmployeeId} on attempt {RetryCount}",
                    transaction.TransactionId,
                    transaction.EmployeeNumber,
                    transaction.HcmEmployeeId,
                    transaction.RetryCount);

                return Ok(
                    MapToResponse(transaction));
            }

            var errorResponse =
                await hcmResponse.Content
                    .ReadFromJsonAsync<
                        HcmErrorResponse>(
                        cancellationToken:
                            cancellationToken);

            var status =
                hcmResponse.StatusCode switch
                {
                    HttpStatusCode.BadRequest =>
                        "ValidationFailed",

                    HttpStatusCode.Conflict =>
                        "Duplicate",

                    HttpStatusCode.ServiceUnavailable =>
                        "TemporaryFailure",

                    HttpStatusCode.InternalServerError =>
                        "Failed",

                    _ =>
                        "Failed"
                };

            await UpdateFailureAsync(
                transaction,
                status,
                errorResponse?.ErrorCode
                    ?? $"HCM-{(int)hcmResponse.StatusCode}",
                errorResponse?.Message
                    ?? "The simulated HCM API returned an error.",
                errorResponse?.Retryable ?? false,
                cancellationToken);

            return Ok(
                MapToResponse(transaction));
        }
        catch (TaskCanceledException)
            when (!cancellationToken
                .IsCancellationRequested)
        {
            await UpdateFailureAsync(
                transaction,
                status: "TimedOut",
                errorCode: "HCM-TIMEOUT-408",
                errorMessage:
                    "The simulated HCM API did not respond within the configured timeout.",
                isRetryable: true,
                cancellationToken);

            return Ok(
                MapToResponse(transaction));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Mock HCM connection failure for transaction {TransactionId}, employee {EmployeeNumber}, attempt {RetryCount}",
                transaction.TransactionId,
                transaction.EmployeeNumber,
                transaction.RetryCount);

            await UpdateFailureAsync(
                transaction,
                status: "TemporaryFailure",
                errorCode:
                    "HCM-CONNECTION-ERROR",
                errorMessage:
                    "The simulated HCM API could not be reached.",
                isRetryable: true,
                cancellationToken);

            return Ok(
                MapToResponse(transaction));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected processing error for transaction {TransactionId}, employee {EmployeeNumber}, attempt {RetryCount}",
                transaction.TransactionId,
                transaction.EmployeeNumber,
                transaction.RetryCount);

            await UpdateFailureAsync(
                transaction,
                status: "Failed",
                errorCode:
                    "ONBOARDING-UNEXPECTED-ERROR",
                errorMessage:
                    "An unexpected error occurred while processing the onboarding transaction.",
                isRetryable: false,
                cancellationToken);

            return Ok(
                MapToResponse(transaction));
        }
    }

    private async Task UpdateFailureAsync(
        OnboardingTransaction transaction,
        string status,
        string errorCode,
        string errorMessage,
        bool isRetryable,
        CancellationToken cancellationToken)
    {
        transaction.Status = status;
        transaction.HcmEmployeeId = null;
        transaction.ErrorCode = errorCode;
        transaction.ErrorMessage = errorMessage;
        transaction.IsRetryable = isRetryable;
        transaction.UpdatedAtUtc =
            DateTime.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        logger.LogWarning(
            "Transaction {TransactionId} processing failed for employee {EmployeeNumber}. Status {Status}, error code {ErrorCode}, retryable {IsRetryable}, attempt {RetryCount}",
            transaction.TransactionId,
            transaction.EmployeeNumber,
            transaction.Status,
            transaction.ErrorCode,
            transaction.IsRetryable,
            transaction.RetryCount);
    }

    private static OnboardingResponse MapToResponse(
        OnboardingTransaction transaction)
    {
        return new OnboardingResponse
        {
            TransactionId =
                transaction.TransactionId,

            EmployeeNumber =
                transaction.EmployeeNumber,

            EmployeeName =
                $"{transaction.FirstName} {transaction.LastName}",

            Status =
                transaction.Status,

            HcmEmployeeId =
                transaction.HcmEmployeeId,

            ErrorCode =
                transaction.ErrorCode,

            ErrorMessage =
                transaction.ErrorMessage,

            IsRetryable =
                transaction.IsRetryable,

            RetryCount =
                transaction.RetryCount,

            CreatedAtUtc =
                transaction.CreatedAtUtc,

            UpdatedAtUtc =
                transaction.UpdatedAtUtc,

            LastAttemptAtUtc =
                transaction.LastAttemptAtUtc
        };
    }
}