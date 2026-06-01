# Copilot Instructions

## General Guidelines

- Section-specific rules (Controllers, API Documentation, Services, Validation, Unit Tests, and Local Testing) always override the General Guidelines.
- Controller classes, controller action methods, and public API endpoints always follow the section-specific documentation rules even when the behavior is obvious.
- For core components or classes with multiple collaborators, include at least a `/// <summary>` and, when the collaboration is not obvious from the code, a `/// <remarks>` that explains the main dependencies or responsibilities.
- For non-controller, non-public-API classes whose name and single responsibility already make the behavior obvious, keep documentation to a one-line `/// <summary>` and omit extra remarks.

## Controllers

- Every controller and every action method **must** have XML documentation comments (`/// <summary>`).
- Controllers should remain thin: perform input validation, then delegate all business logic to a service.
- Do not embed business logic directly in controllers.
- Controllers must not catch `Exception` or duplicate business-layer error handling. Rely on centralized exception-handling middleware or filters to translate known service exceptions into the HTTP status codes declared by `[ProducesResponseType]`.
- Do **NOT** reference the UI project (AdminUI) from the API project. 

## API Documentation (Swagger / OpenAPI)

- All exposed API endpoints must have Swagger documentation.
- Use `[ProducesResponseType]` attributes to document all possible response codes.
- XML documentation comments on action methods must be surfaced in Swagger by enabling the XML comments file in the OpenAPI configuration.
- Avoid using generic/boilerplate parameter documentation like "Optional cancellation token." Instead, parameter docs should explain what the parameter does in the context of the specific method.

## Async, Cancellation, and Logging

- All I/O-bound controller and service methods should be asynchronous.
- All I/O-bound controller and service methods must accept a `CancellationToken` when the surrounding framework supports it and must pass that token to downstream calls.
- Use `ILogger<T>` for structured logging. Do not use `Console.WriteLine` for application logging.

Example action method:

```csharp
/// <summary>
/// Retrieves a client by their unique identifier.
/// </summary>
/// <param name="id">The unique identifier of the client.</param>
/// <returns>The client matching the given identifier.</returns>
/// <response code="200">Returns the requested client.</response>
/// <response code="404">No client was found with the given identifier.</response>
[HttpGet("{id}")]
[ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetById(int id) { ... }
```

## Services

- One service, one goal: each service class must have a single, clearly defined responsibility.
- Do not create large, catch-all services. Split by domain concern (e.g., `ClientService`, `InvoiceService`).
- Service interfaces must be defined and used for dependency injection.
  When documenting service interfaces, do not reference other specific service classes. Document the interface with what it does and what it's responsible for. On the implementing class, you can reference other implementations more freely. Cross-referencing other interfaces from an interface is OK.
- Services throw domain-specific exceptions such as `NotFoundException` or `ConflictException` for failure cases instead of returning ambiguous success or failure values. Controllers translate these to the appropriate HTTP responses through a global exception filter or centralized exception-handling middleware, mapping them to the response codes declared by `[ProducesResponseType]`.

## Validation

- Input validation belongs in the controller layer (model validation attributes, `ModelState`, or `FluentValidation`).
- Services may assume valid input and should not duplicate controller-level validation.

## Unit Tests

- Place automated tests in the corresponding `*.Tests` project for the layer under test.
- Name tests using the `MethodName_StateUnderTest_ExpectedBehavior` pattern.
- Each service should have unit tests that cover its main success and failure paths with dependencies mocked or substituted as appropriate.

## Local Testing

When starting the application for testing or verification:

1. **Start the Storage API** — run `ClientManager.StorageApi` (default: `http://localhost:5063`). This host is the only app that talks to `ClientManager.DataAccess`. If it fails to start, inspect its logs and do not continue until it is listening on its configured port.
2. **Start the API** — run `ClientManager.Api` (default: `http://localhost:5062`). It depends on the Storage API being available first. If it fails to start, inspect its logs and do not continue until it is listening on its configured port.
3. **Start the Admin UI** — run `ClientManager.AdminUI` (default: `http://localhost:5100`).
4. **Seed data if empty** — run `python _scripts/seed_data.py --base-url http://localhost:5062`. Creates services, resource pools, global rate limits, and client configurations through the public API. Safe to re-run (existing items are skipped). If `python` is not on `PATH` or the script exits with a non-zero code, stop the sequence and report the failure before continuing.
5. **Start the traffic generator** — run `python _scripts/traffic_generator.py --base-url http://localhost:5062 --interval 2.0` in a new background VS Code terminal and do not wait for it to finish. It sends continuous realistic traffic (access checks, resource acquire/release, reads) so the dashboard shows live data. If `python` is not on `PATH` or the script exits with a non-zero code, stop the sequence and report the failure before continuing.
6. **Shut down** — stop the traffic generator first (Ctrl+C), then stop the API, Storage API, and Admin UI.

**Shutdown order is critical.** The traffic generator sends continuous HTTP requests to the API. If the API is stopped while the traffic generator is still running, the generator floods the terminal with connection-refused errors that are noisy and slow to cancel. Always stop the traffic generator **before** the API. Stop the Storage API after the API so the public host does not spend shutdown time failing internal calls.
