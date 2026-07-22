using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Onboarding.Api.Data;
using Onboarding.Api.Entities;
using Onboarding.Api.Models;
using Onboarding.Api.Services;

namespace Onboarding.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(
    OnboardingDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<OnboardingController> logger,
    IAiFailureExplanationService aiFailureExplanationService)
    : ControllerBase
{
    private const int MaximumProcessingAttempts = 3;

    [HttpPost]
    [RequestSizeLimit(65_536)]
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

        var transaction =
            new OnboardingTransaction
            {
                TransactionId =
                    Guid.NewGuid(),

                EmployeeNumber =
                    employeeNumber,

                FirstName =
                    request.FirstName.Trim(),

                LastName =
                    request.LastName.Trim(),

                Email =
                    request.Email
                        .Trim()
                        .ToLowerInvariant(),

                Department =
                    request.Department.Trim(),

                Country =
                    request.Country.Trim(),

                JoiningDate =
                    request.JoiningDate,

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

        dbContext.OnboardingTransactions.Add(
            transaction);

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
    typeof(PagedResponse<OnboardingResponse>),
    StatusCodes.Status200OK)]
[ProducesResponseType(
    StatusCodes.Status400BadRequest)]
public async Task<ActionResult<PagedResponse<OnboardingResponse>>>
    GetAll(
        [FromQuery] OnboardingQueryParameters parameters,
        CancellationToken cancellationToken)
{
    var query =
        dbContext.OnboardingTransactions
            .AsNoTracking()
            .AsQueryable();

    if (!string.IsNullOrWhiteSpace(
            parameters.Status))
    {
        var normalizedStatus =
            parameters.Status.Trim();

        query =
            query.Where(
                transaction =>
                    transaction.Status ==
                    normalizedStatus);
    }

    if (!string.IsNullOrWhiteSpace(
            parameters.Search))
    {
        var normalizedSearch =
            parameters.Search
                .Trim()
                .ToUpperInvariant();

        query =
            query.Where(
                transaction =>
                    transaction.EmployeeNumber
                        .ToUpper()
                        .Contains(normalizedSearch) ||
                    transaction.FirstName
                        .ToUpper()
                        .Contains(normalizedSearch) ||
                    transaction.LastName
                        .ToUpper()
                        .Contains(normalizedSearch));
    }

    var totalItems =
        await query.CountAsync(
            cancellationToken);

    var totalPages =
        totalItems == 0
            ? 0
            : (int)Math.Ceiling(
                totalItems /
                (double)parameters.PageSize);

    var items =
        await query
            .OrderByDescending(
                transaction =>
                    transaction.CreatedAtUtc)
            .Skip(
                (parameters.PageNumber - 1) *
                parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(
                transaction =>
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

    var response =
        new PagedResponse<OnboardingResponse>
        {
            Items =
                items,

            PageNumber =
                parameters.PageNumber,

            PageSize =
                parameters.PageSize,

            TotalItems =
                totalItems,

            TotalPages =
                totalPages
        };

    return Ok(response);
}

   [HttpGet("summary")]
[ProducesResponseType(
    typeof(OnboardingSummaryResponse),
    StatusCodes.Status200OK)]
public async Task<ActionResult<OnboardingSummaryResponse>>
    GetSummary(
        CancellationToken cancellationToken)
{
    var transactions =
        dbContext.OnboardingTransactions
            .AsNoTracking();

    var totalTransactions =
        await transactions.CountAsync(
            cancellationToken);

    var pendingTransactions =
        await transactions.CountAsync(
            transaction =>
                transaction.Status == "Pending",
            cancellationToken);

    var processingTransactions =
        await transactions.CountAsync(
            transaction =>
                transaction.Status == "Processing",
            cancellationToken);

    var completedTransactions =
        await transactions.CountAsync(
            transaction =>
                transaction.Status == "Completed",
            cancellationToken);

    var retryableFailures =
        await transactions.CountAsync(
            transaction =>
                transaction.IsRetryable,
            cancellationToken);

    var retryLimitExceededTransactions =
        await transactions.CountAsync(
            transaction =>
                transaction.Status ==
                "RetryLimitExceeded",
            cancellationToken);

    var failedTransactions =
        await transactions.CountAsync(
            transaction =>
                transaction.Status ==
                    "ValidationFailed" ||
                transaction.Status ==
                    "Duplicate" ||
                transaction.Status ==
                    "TemporaryFailure" ||
                transaction.Status ==
                    "Failed" ||
                transaction.Status ==
                    "TimedOut" ||
                transaction.Status ==
                    "RetryLimitExceeded",
            cancellationToken);

    var response =
        new OnboardingSummaryResponse
        {
            TotalTransactions =
                totalTransactions,

            PendingTransactions =
                pendingTransactions,

            ProcessingTransactions =
                processingTransactions,

            CompletedTransactions =
                completedTransactions,

            FailedTransactions =
                failedTransactions,

            RetryableFailures =
                retryableFailures,

            RetryLimitExceededTransactions =
                retryLimitExceededTransactions,

            GeneratedAtUtc =
                DateTime.UtcNow
        };

    logger.LogInformation(
        "Onboarding dashboard summary generated. Total {TotalTransactions}, completed {CompletedTransactions}, failed {FailedTransactions}, retryable {RetryableFailures}",
        response.TotalTransactions,
        response.CompletedTransactions,
        response.FailedTransactions,
        response.RetryableFailures);

    return Ok(response);
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
    [EnableRateLimiting("ProcessingPolicy")]
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
        var existingTransaction =
            await dbContext.OnboardingTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    transaction =>
                        transaction.TransactionId ==
                        transactionId,
                    cancellationToken);

        if (existingTransaction is null)
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
                existingTransaction.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Initial processing rejected for transaction {TransactionId}, employee {EmployeeNumber}, current status {Status}",
                existingTransaction.TransactionId,
                existingTransaction.EmployeeNumber,
                existingTransaction.Status);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' cannot be processed because its current status is '{existingTransaction.Status}'. Use the retry endpoint for retryable failures."
            });
        }

        var claimedTransaction =
            await TryClaimTransactionAsync(
                transactionId,
                requiredStatus: "Pending",
                requireRetryable: false,
                cancellationToken);

        if (claimedTransaction is null)
        {
            logger.LogWarning(
                "Concurrent processing claim rejected for transaction {TransactionId}",
                transactionId);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' is already being processed or its status was changed by another request."
            });
        }

        logger.LogInformation(
            "Initial processing claimed for transaction {TransactionId}, employee {EmployeeNumber}, attempt {RetryCount}",
            claimedTransaction.TransactionId,
            claimedTransaction.EmployeeNumber,
            claimedTransaction.RetryCount);

        return await ProcessTransactionAsync(
            claimedTransaction,
            cancellationToken);
    }

    [HttpPost("{transactionId:guid}/retry")]
    [EnableRateLimiting("ProcessingPolicy")]
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
        var existingTransaction =
            await dbContext.OnboardingTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    transaction =>
                        transaction.TransactionId ==
                        transactionId,
                    cancellationToken);

        if (existingTransaction is null)
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
                existingTransaction.Status,
                "Completed",
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Retry rejected for completed transaction {TransactionId}, employee {EmployeeNumber}",
                existingTransaction.TransactionId,
                existingTransaction.EmployeeNumber);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' has already been completed and cannot be retried."
            });
        }

        if (!existingTransaction.IsRetryable)
        {
            logger.LogWarning(
                "Retry rejected for transaction {TransactionId}, employee {EmployeeNumber}, status {Status}, error code {ErrorCode}",
                existingTransaction.TransactionId,
                existingTransaction.EmployeeNumber,
                existingTransaction.Status,
                existingTransaction.ErrorCode);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' is not eligible for retry because its failure is non-retryable."
            });
        }

        if (existingTransaction.RetryCount >=
            MaximumProcessingAttempts)
        {
            await dbContext.OnboardingTransactions
                .Where(
                    transaction =>
                        transaction.TransactionId ==
                            transactionId &&
                        transaction.IsRetryable &&
                        transaction.RetryCount >=
                            MaximumProcessingAttempts)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                transaction =>
                                    transaction.Status,
                                "RetryLimitExceeded")
                            .SetProperty(
                                transaction =>
                                    transaction.IsRetryable,
                                false)
                            .SetProperty(
                                transaction =>
                                    transaction.UpdatedAtUtc,
                                DateTime.UtcNow),
                    cancellationToken);

            dbContext.ChangeTracker.Clear();

            logger.LogWarning(
                "Retry limit reached for transaction {TransactionId}, employee {EmployeeNumber}, retry count {RetryCount}",
                existingTransaction.TransactionId,
                existingTransaction.EmployeeNumber,
                existingTransaction.RetryCount);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' has reached the maximum limit of {MaximumProcessingAttempts} processing attempts."
            });
        }

        var claimedTransaction =
            await TryClaimTransactionAsync(
                transactionId,
                requiredStatus:
                    existingTransaction.Status,
                requireRetryable: true,
                cancellationToken);

        if (claimedTransaction is null)
        {
            logger.LogWarning(
                "Concurrent retry claim rejected for transaction {TransactionId}",
                transactionId);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' is already being processed or was changed by another request."
            });
        }

        logger.LogInformation(
            "Retry claimed for transaction {TransactionId}, employee {EmployeeNumber}, attempt {RetryCount}",
            claimedTransaction.TransactionId,
            claimedTransaction.EmployeeNumber,
            claimedTransaction.RetryCount);

        return await ProcessTransactionAsync(
            claimedTransaction,
            cancellationToken);
    }

    [HttpPost("{transactionId:guid}/explain")]
    [EnableRateLimiting("AiPolicy")]
    [ProducesResponseType(
        typeof(AiFailureExplanationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<
        ActionResult<AiFailureExplanationResponse>>
        ExplainFailure(
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
                "AI explanation rejected because transaction {TransactionId} was not found",
                transactionId);

            return NotFound(new
            {
                message =
                    $"Transaction '{transactionId}' was not found."
            });
        }

        var unsupportedStatuses =
            new[]
            {
                "Pending",
                "Processing",
                "Completed"
            };

        var explanationNotRequired =
            unsupportedStatuses.Any(
                status =>
                    string.Equals(
                        status,
                        transaction.Status,
                        StringComparison.OrdinalIgnoreCase));

        if (explanationNotRequired)
        {
            logger.LogWarning(
                "AI explanation rejected for transaction {TransactionId} because status {Status} does not represent a failure",
                transaction.TransactionId,
                transaction.Status);

            return Conflict(new
            {
                message =
                    $"AI failure explanation is not available because transaction '{transactionId}' has status '{transaction.Status}'."
            });
        }

        if (string.IsNullOrWhiteSpace(
                transaction.ErrorCode) &&
            string.IsNullOrWhiteSpace(
                transaction.ErrorMessage))
        {
            logger.LogWarning(
                "AI explanation rejected for transaction {TransactionId} because no failure details are available",
                transaction.TransactionId);

            return Conflict(new
            {
                message =
                    $"Transaction '{transactionId}' does not contain failure details that can be explained."
            });
        }

        try
        {
            var explanation =
                await aiFailureExplanationService
                    .ExplainAsync(
                        transaction,
                        cancellationToken);

            return Ok(explanation);
        }
        catch (TaskCanceledException)
            when (!cancellationToken
                .IsCancellationRequested)
        {
            logger.LogWarning(
                "AI explanation timed out for transaction {TransactionId}",
                transaction.TransactionId);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "The local AI model did not respond within the configured timeout."
                });
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Local AI service could not be reached for transaction {TransactionId}",
                transaction.TransactionId);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "The local AI service is currently unavailable. Ensure Ollama is running."
                });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Local AI returned an invalid response for transaction {TransactionId}",
                transaction.TransactionId);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "The local AI service returned an invalid or empty response."
                });
        }
    }

    private async Task<OnboardingTransaction?>
        TryClaimTransactionAsync(
            Guid transactionId,
            string requiredStatus,
            bool requireRetryable,
            CancellationToken cancellationToken)
    {
        var attemptTime =
            DateTime.UtcNow;

        var query =
            dbContext.OnboardingTransactions
                .Where(
                    transaction =>
                        transaction.TransactionId ==
                            transactionId &&
                        transaction.Status ==
                            requiredStatus &&
                        transaction.RetryCount <
                            MaximumProcessingAttempts);

        if (requireRetryable)
        {
            query =
                query.Where(
                    transaction =>
                        transaction.IsRetryable);
        }

        var affectedRows =
            await query.ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(
                            transaction =>
                                transaction.Status,
                            "Processing")
                        .SetProperty(
                            transaction =>
                                transaction.RetryCount,
                            transaction =>
                                transaction.RetryCount + 1)
                        .SetProperty(
                            transaction =>
                                transaction.LastAttemptAtUtc,
                            attemptTime)
                        .SetProperty(
                            transaction =>
                                transaction.UpdatedAtUtc,
                            attemptTime)
                        .SetProperty(
                            transaction =>
                                transaction.ErrorCode,
                            (string?)null)
                        .SetProperty(
                            transaction =>
                                transaction.ErrorMessage,
                            (string?)null)
                        .SetProperty(
                            transaction =>
                                transaction.IsRetryable,
                            false),
                cancellationToken);

        if (affectedRows != 1)
        {
            return null;
        }

        dbContext.ChangeTracker.Clear();

        return await dbContext.OnboardingTransactions
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.TransactionId ==
                        transactionId &&
                    transaction.Status ==
                        "Processing",
                cancellationToken);
    }

    private async Task<ActionResult<OnboardingResponse>>
        ProcessTransactionAsync(
            OnboardingTransaction transaction,
            CancellationToken cancellationToken)
    {
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

                transaction.Status =
                    "Completed";

                transaction.HcmEmployeeId =
                    successResponse.HcmEmployeeId;

                transaction.ErrorCode =
                    null;

                transaction.ErrorMessage =
                    null;

                transaction.IsRetryable =
                    false;

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
                errorCode:
                    "HCM-TIMEOUT-408",
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
        transaction.Status =
            status;

        transaction.HcmEmployeeId =
            null;

        transaction.ErrorCode =
            errorCode;

        transaction.ErrorMessage =
            errorMessage;

        transaction.IsRetryable =
            isRetryable;

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