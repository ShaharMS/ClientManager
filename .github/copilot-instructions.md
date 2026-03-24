# Copilot Instructions

## General Guidelines

- Use extensive documentation for core components or those with multiple purposes/complex relationships.
- Keep documentation light or skip it if the class's purpose is already clear or very specific. Avoid over-documenting obvious things.

## Controllers

- Every controller and every action method **must** have XML documentation comments (`/// <summary>`).
- Controllers should remain thin: perform input validation, then delegate all business logic to a service.
- Do not embed business logic directly in controllers.

## API Documentation (Swagger / OpenAPI)

- All exposed API endpoints must have Swagger documentation.
- Use `[ProducesResponseType]` attributes to document all possible response codes.
- XML documentation comments on action methods must be surfaced in Swagger by enabling the XML comments file in the OpenAPI configuration.

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

## Validation

- Input validation belongs in the controller layer (model validation attributes, `ModelState`, or `FluentValidation`).
- Services may assume valid input and should not duplicate controller-level validation.
