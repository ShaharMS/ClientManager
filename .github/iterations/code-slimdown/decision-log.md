# Decision Log — code-slimdown

## Accepted Waivers

None.

## Open Decisions

None.

## Notes

- Step 1 / logger collapse: chose single method per level `X(string message, object? extraData = null, Exception? exception = null)` and updated existing exception call sites (which passed the exception positionally as the second argument) to keep the exception bound to the `exception` parameter rather than `extraData`. This realizes the plan's "one method per level" intent without the silent behavior change the plan assumed would not occur.
