using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using MockHcm.Api.Models;

namespace MockHcm.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, CreateEmployeeResponse>
        Employees = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, int>
    ProcessingAttempts =
        new(StringComparer.OrdinalIgnoreCase);

    [HttpPost]
    [ProducesResponseType(
        typeof(CreateEmployeeResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CreateEmployeeResponse>> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var employeeNumber = request.EmployeeNumber
            .Trim()
            .ToUpperInvariant();

        // Simulate a business validation failure.
        if (employeeNumber.EndsWith("400"))
        {
            return BadRequest(new
            {
                errorCode = "HCM-VALIDATION-400",
                message =
                    "The simulated HCM system rejected the employee data.",
                retryable = false
            });
        }

        // Simulate an internal server failure.
        if (employeeNumber.EndsWith("500"))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    errorCode = "HCM-SERVER-500",
                    message =
                        "The simulated HCM system encountered an internal error.",
                    retryable = true
                });
        }

        // Simulate a temporarily unavailable service.
        if (employeeNumber.EndsWith("503"))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    errorCode = "HCM-UNAVAILABLE-503",
                    message =
                        "The simulated HCM system is temporarily unavailable.",
                    retryable = true
                });
        }

        /*
 * Employee numbers ending in 603 simulate a recoverable
 * temporary failure.
 *
 * Attempts 1 and 2 return HTTP 503.
 * Attempt 3 succeeds.
 */
if (employeeNumber.EndsWith("603"))
{
    var currentAttempt =
        ProcessingAttempts.AddOrUpdate(
            employeeNumber,
            addValue: 1,
            updateValueFactory:
                (_, existingAttempt) =>
                    existingAttempt + 1);

    if (currentAttempt <= 2)
    {
        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new
            {
                errorCode =
                    "HCM-RECOVERABLE-503",

                message =
                    $"The simulated HCM system is temporarily unavailable. Attempt {currentAttempt} of 3.",

                retryable = true,

                attemptNumber =
                    currentAttempt
            });
    }
}

        // Simulate a slow HCM response.
        if (employeeNumber.EndsWith("408"))
        {
            await Task.Delay(
                TimeSpan.FromSeconds(10),
                cancellationToken);
        }

        if (Employees.ContainsKey(employeeNumber))
        {
            return Conflict(new
            {
                errorCode = "HCM-DUPLICATE-409",
                message =
                    $"Employee '{employeeNumber}' already exists in the simulated HCM system.",
                retryable = false
            });
        }

        var hcmIdentifier =
            $"HCM-{Guid.NewGuid():N}".ToUpperInvariant();

        var response = new CreateEmployeeResponse
        {
            HcmEmployeeId = hcmIdentifier[..16],
            EmployeeNumber = employeeNumber,
            Status = "Created",
            Message =
                "Employee created successfully in the simulated HCM system.",
            CreatedAtUtc = DateTime.UtcNow
        };

        Employees.TryAdd(employeeNumber, response);

        return CreatedAtAction(
            nameof(GetEmployee),
            new { employeeNumber },
            response);
    }

    [HttpGet("{employeeNumber}")]
    [ProducesResponseType(
        typeof(CreateEmployeeResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CreateEmployeeResponse> GetEmployee(
        string employeeNumber)
    {
        var normalizedEmployeeNumber = employeeNumber
            .Trim()
            .ToUpperInvariant();

        if (!Employees.TryGetValue(
                normalizedEmployeeNumber,
                out var employee))
        {
            return NotFound(new
            {
                message =
                    $"Employee '{normalizedEmployeeNumber}' was not found in the simulated HCM system."
            });
        }

        return Ok(employee);
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyCollection<CreateEmployeeResponse>),
        StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<CreateEmployeeResponse>>
        GetAllEmployees()
    {
        var employees = Employees.Values
            .OrderByDescending(employee => employee.CreatedAtUtc)
            .ToList();

        return Ok(employees);
    }
}