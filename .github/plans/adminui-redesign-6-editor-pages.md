# Plan: AdminUI Redesign — Step 6: Editor Pages

> **Status**: 🔲 Not started
> **Prerequisite**: [adminui-redesign-5-list-pages.md](adminui-redesign-5-list-pages.md)
> **Next**: None — this is the final step.
> **Parent**: [adminui-redesign-overview.md](adminui-redesign-overview.md)

## TL;DR

Restyle all editor/form pages (Client, Service, Resource Pool, Global Rate Limit) using Radzen form components (`RadzenTextBox`, `RadzenNumeric`, `RadzenSwitch`, `RadzenDropDown`) inside the card-based Metric visual style. Keep existing validation and form logic intact.

## Reference Pattern

**Design Reference**: [Dribbble Metric Dashboard Video](https://cdn.dribbble.com/userupload/42834013/file/original-333c4f78536a41262709503fae3c7342.mp4)

The reference doesn't show editor forms, but we match the overall aesthetic: white card containers, rounded inputs, clean spacing, and the indigo/purple action buttons.

In [ClientManager.AdminUI/Components/Pages/Clients/ClientEditor.razor](../../ClientManager.AdminUI/Components/Pages/Clients/ClientEditor.razor):
- Most complex editor: nested sections for global rate limit, service access, resource pools
- Uses `EditForm` with `DataAnnotationsValidator`
- Dynamic add/remove for service entries and pool entries
- This is the template all other editors should follow visually

In [ClientManager.AdminUI/Components/Pages/Services/ServiceEditor.razor](../../ClientManager.AdminUI/Components/Pages/Services/ServiceEditor.razor):
- Simplest editor: ID, Name, IsEnabled — good baseline reference

## Steps

### 1. Create editor page CSS

Create `ClientManager.AdminUI/wwwroot/css/editor-pages.css`:

```css
.cm-editor {
    max-width: 800px;
}

.cm-editor__card {
    background: var(--color-bg-card);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-sm);
    border: 1px solid var(--color-border);
    padding: var(--space-xl);
    margin-bottom: var(--space-lg);
}

.cm-editor__section-title {
    font-size: var(--font-size-md);
    font-weight: 600;
    color: var(--color-text-primary);
    margin-bottom: var(--space-md);
    padding-bottom: var(--space-sm);
    border-bottom: 1px solid var(--color-border);
}

.cm-editor__field {
    margin-bottom: var(--space-md);
}

.cm-editor__field label {
    display: block;
    font-size: var(--font-size-sm);
    font-weight: 500;
    color: var(--color-text-secondary);
    margin-bottom: var(--space-xs);
}

.cm-editor__actions {
    display: flex;
    gap: var(--space-sm);
    margin-top: var(--space-lg);
}

.cm-editor__nested-item {
    background: var(--color-bg);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm);
    padding: var(--space-md);
    margin-bottom: var(--space-sm);
}

.cm-editor__nested-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--space-sm);
}
```

Reference in `App.razor` `<head>`:

```html
<link rel="stylesheet" href="css/editor-pages.css">
```

### 2. Restyle `ServiceEditor.razor` (simplest form)

Replace Bootstrap form elements with Radzen components:

```razor
<div class="cm-page-header">
    <h1>@(_isEdit ? "Edit Service" : "Create Service")</h1>
    <p>@(_isEdit ? "Update service configuration." : "Define a new service.")</p>
</div>

<div class="cm-editor">
    <EditForm Model="@_model" OnValidSubmit="HandleSubmit">
        <DataAnnotationsValidator />

        <div class="cm-editor__card">
            <div class="cm-editor__field">
                <label>Service ID</label>
                <RadzenTextBox @bind-Value="_model.Id" Disabled="@_isEdit" Style="width: 100%;" />
            </div>
            <div class="cm-editor__field">
                <label>Name</label>
                <RadzenTextBox @bind-Value="_model.Name" Style="width: 100%;" />
            </div>
            <div class="cm-editor__field">
                <label>Enabled</label>
                <RadzenSwitch @bind-Value="_model.IsEnabled" />
            </div>
        </div>

        <div class="cm-editor__actions">
            <RadzenButton Text="Save" ButtonType="ButtonType.Submit" ButtonStyle="ButtonStyle.Primary" />
            <RadzenButton Text="Cancel" Variant="Variant.Outlined" Click="@(() => Nav.NavigateTo("services"))" />
        </div>
    </EditForm>
</div>
```

### 3. Restyle `ResourcePoolEditor.razor`

Same pattern as ServiceEditor with fields: ID, Name, MaxSlots (`RadzenNumeric`), AllocationTtlSeconds (`RadzenNumeric`).

### 4. Restyle `GlobalRateLimitEditor.razor`

Same card layout with:
- ID (`RadzenTextBox`, disabled on edit)
- Target ID (`RadzenTextBox`)
- Target Type (`RadzenDropDown` with Service/ResourcePool options)
- Strategy (`RadzenDropDown` with FixedWindow/ApproximateSlidingWindow/TokenBucket)
- Max Requests (`RadzenNumeric`)
- Window in seconds (`RadzenNumeric`)
- Tokens Per Refill (`RadzenNumeric`, conditionally visible when Strategy = TokenBucket)

### 5. Restyle `ClientEditor.razor` (most complex)

This is the most complex form. Structure it as multiple card sections:

**Card 1 — Basic Configuration**:
- ID, Name, IsEnabled, ContributesToGlobalLimits, ExemptFromGlobalLimits

**Card 2 — Global Rate Limit** (collapsible/toggle):
- Toggle switch to enable/disable the section
- When enabled: Strategy dropdown, MaxRequests, Window, TokensPerRefill (conditional)

**Card 3 — Service Access** (dynamic list):
- "Add Service" button at top
- Each entry in a `cm-editor__nested-item`: Service ID, IsAllowed switch, Rate Limit toggle, and conditional rate limit fields
- Remove button per entry

**Card 4 — Resource Pools** (dynamic list):
- "Add Pool" button at top
- Each entry: Pool ID, MaxSlots numeric
- Remove button per entry

Use `RadzenButton` with `Icon="add"` for add buttons and `Icon="delete"` for remove buttons.

### 6. Add back navigation breadcrumb

Add a simple back link at the top of each editor page:

```razor
<div style="margin-bottom: var(--space-md);">
    <RadzenLink Path="services" Text="← Back to Services" Style="font-size: var(--font-size-sm);" />
</div>
```

## Verification

- All editor page files compile without errors

### Required: Browser Verification

Before marking this step complete, the implementer **must**:
1. Ensure the API project is running in a background terminal (start it if not already running).
2. Ensure the AdminUI project is running in a background terminal (restart it to pick up changes).
3. Open the AdminUI in the shared browser and navigate to **each** of the 4 editor pages (create mode). For each, take a screenshot and verify:
   - Card-based styling with rounded corners and shadows
   - All input fields use Radzen components (TextBox, Numeric, Switch, DropDown)
   - Submit/Cancel buttons are styled with Radzen ButtonStyle
4. On the **Client Editor** specifically:
   - Click "Add Service" and "Add Pool" buttons and screenshot to verify dynamic add/remove works
   - Select TokenBucket strategy and screenshot to verify conditional fields appear
5. On the **Global Rate Limit Editor**:
   - Select TokenBucket strategy and screenshot to verify Tokens Per Refill field appears
6. Test a full create-then-edit flow on the **Service Editor**: fill in a form, submit, verify redirect to list, click edit on the same item, verify data loaded back.
7. Verify the back/cancel navigation returns to the correct list page.
8. Share all screenshots with the user for sign-off. This is the final step — confirm the full redesign is complete.
