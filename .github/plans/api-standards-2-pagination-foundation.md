# Plan: API Standards — Step 2: Pagination Foundation

> **Status**: ✅ Completed
> **Prerequisite**: [api-standards-1-versioning.md](api-standards-1-versioning.md)
> **Next**: [api-standards-3-apply-pagination-filtering.md](api-standards-3-apply-pagination-filtering.md)
> **Parent**: [api-standards-overview.md](api-standards-overview.md)

## TL;DR

Create the shared pagination request/response models (`PagedRequest`, `PagedResponse<T>`) and a LINQ extension method to paginate any `IReadOnlyList<T>` in-memory. These types form the foundation that Step 3 applies to every list endpoint.

## Reference Pattern

In [ClientManager.Api/Models/Responses/StatisticsResponses.cs](ClientManager.Api/Models/Responses/StatisticsResponses.cs):
- Response models are `record` types in the `ClientManager.Api.Models.Responses` namespace
- Concise positional record syntax with XML doc comments

In [ClientManager.Api/Models/Requests/CheckAccessRequest.cs](ClientManager.Api/Models/Requests/CheckAccessRequest.cs):
- Request models live under `ClientManager.Api.Models.Requests`

## Steps

### 1. Create `PagedRequest` record

Create `ClientManager.Api/Models/Requests/PagedRequest.cs`. A positional record with:
- `Page` (int, default 1) and `PageSize` (int, default 20) parameters
- A `Clamp()` method that returns a validated copy: `Page` clamped to `>= 1`, `PageSize` clamped to `[1, 100]`

### 2. Create `PagedResponse<T>` record

Create `ClientManager.Api/Models/Responses/PagedResponse.cs`. A generic positional record with:
- `Items` (`IReadOnlyList<T>`), `Page`, `PageSize`, `TotalCount`, `TotalPages`

### 3. Create pagination extension method

Create `ClientManager.Api/Extensions/PaginationExtensions.cs`. A static class with a `ToPagedResponse<T>` extension method on `IReadOnlyList<T>` that:
- Accepts a `PagedRequest`, calls `Clamp()` on it
- Computes `totalCount`, `totalPages` (ceiling division)
- Applies `.Skip()/.Take()` to produce the page
- Returns a `PagedResponse<T>`
```

## Verification

- Project compiles without errors (`dotnet build`).
- `PagedRequest`, `PagedResponse<T>`, and `ToPagedResponse` are importable from their respective namespaces.
- No existing endpoints are changed — this step only adds new types.
- `PagedRequest.Clamp()` correctly handles edge cases: `Page=0` → 1, `PageSize=200` → 100, `PageSize=-5` → 1.
