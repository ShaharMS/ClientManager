# Plan: Address ClientManager.Api Review Notes

## Status: 🔄 In progress (Step 2 of 5 complete)

## Overview

The review notes in [../../Code Review - 1.0.0-alpha.md](../../Code%20Review%20-%201.0.0-alpha.md) and the inline `CR:` markers across `ClientManager.Api` point to the same underlying problem: the API host still mixes transport details, controller responsibilities, configuration parsing, and documentation concerns in ways that make the public surface harder to reason about. Controllers still inject storage-facing clients directly, transport contracts are encoded as local strings and ad-hoc helper methods, error mapping is split between nullable returns and middleware casework, and Swagger only partially reflects the actual API contract.

The target state is a clearer public host that keeps `ClientManager.Api` thin and documented while still remaining independent from `ClientManager.DataAccess`. Immutable cross-service route/query contracts should live in `ClientManager.Shared`, environment-specific settings should live in typed options, controllers should delegate to API services instead of storage-facing clients, and the error pipeline should center on typed HTTP/problem exceptions rather than nullable returns plus controller-side `?? throw` patterns. This plan follows the service-boundary pattern already present in the runtime access/resource endpoints, the existing Swagger tag/document filter setup, and the documented shared request/response model pattern already used in `ClientManager.Shared`.

## Sub-Plans (execute in order)

| Order | Plan File | Summary |
|-------|-----------|---------|
| 1 | [api-cr-remediation-1-foundation-contracts.md](.github/plans/api-cr-remediation-1-foundation-contracts.md) | Extract shared API/storage contracts, replace ad-hoc query parsing, and clean up typed option binding so startup/configuration are explicit. |
| 2 | [api-cr-remediation-2-http-exception-pipeline.md](.github/plans/api-cr-remediation-2-http-exception-pipeline.md) | Replace nullable-plus-controller-throw patterns with a typed HTTP exception flow and simplify the error middleware around that contract. |
| 3 | [api-cr-remediation-3-internal-transport-structure.md](.github/plans/api-cr-remediation-3-internal-transport-structure.md) | Reorganize the internal transport layer, flatten folder nesting, rename ambiguous helpers, and document the storage-facing API infrastructure. |
| 4 | [api-cr-remediation-4-services-and-controllers.md](.github/plans/api-cr-remediation-4-services-and-controllers.md) | Move remaining controller/business responsibilities into API services and standardize controller response style, validation, and naming. |
| 5 | [api-cr-remediation-5-openapi-and-documentation.md](.github/plans/api-cr-remediation-5-openapi-and-documentation.md) | Finish the Swagger/XML documentation pass so operations, failure responses, and shared schemas render correctly in the public API docs. |

## Key Decisions

- **Static contracts go to `ClientManager.Shared`** — Immutable route fragments, query-parameter names, and request-shape helpers used across hosts should move into shared code, while host-specific values such as base URLs and timeouts remain in typed options. This resolves the project-wide reuse concern more cleanly than putting fixed route strings in appsettings.
- **`ClientManager.Api` and shared libraries only** — This plan may touch `ClientManager.Api` and `ClientManager.Shared`, but it intentionally does not require edits in `ClientManager.StorageApi`. Any storage-host counterpart work should be generated from this plan later as a parallel CR.
- **Controllers depend on API services, not internal clients** — The runtime endpoints already show the desired public shape: controllers remain thin and public-service interfaces own storage-facing orchestration.
- **Typed HTTP/problem exceptions become the single public error contract** — Expected failures should carry status/title/detail at the exception level so the middleware can focus on logging policy and RFC 7807 output instead of hand-mapping every concrete type.
- **Documentation follows settled shapes** — Structural and service-boundary changes land before the final Swagger/schema sweep so XML docs and response metadata are authored against the final namespaces, interfaces, and route contracts.

## Iteration Bootstrap Metadata

- **Recommended iteration slug**: `api-cr-remediation`
- **Evidence to preserve**: `dotnet build` output for `ClientManager.Api` and `ClientManager.Shared`; screenshots or notes from `http://localhost:5062/docs`; browser checks on `/`, `/clients`, `/services`, `/resource-pools`, `/rate-limits`, and `/monitor`.
- **Review focus**: no controller should inject `Services.InternalClients.*`; no hard-coded cross-host route/query strings should remain outside approved shared contract types; middleware and exceptions should agree on status/title/detail ownership; Swagger should show shared schema descriptions rather than only endpoint titles.
- **Commit guidance**: keep shared-contract/options work separate from transport-folder moves, keep service/controller migrations separate from the docs-only pass, and avoid mixing namespace churn with Swagger annotation churn in the same commit when possible.