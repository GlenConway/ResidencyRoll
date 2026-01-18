# GitHub Copilot Instructions for ResidencyRoll

## Blazor Component Code-Behind Pattern

When creating or modifying Blazor components in this project, **ALWAYS** use the code-behind pattern:

### Structure
- **`.razor` file**: Contains ONLY markup (HTML/Razor syntax), directives (`@page`, `@using`, `@inject`, `@attribute`, `@rendermode`), and component references
- **`.razor.cs` file**: Contains ALL C# code including:
  - Class definition as `public partial class ComponentName`
  - All parameters (`[Parameter]`)
  - All injected services (`[Inject]`)
  - All private fields and properties
  - All methods (lifecycle methods like `OnInitializedAsync`, event handlers, helper methods)
  - Nested classes if needed

### Example

#### ✅ CORRECT: Component.razor
```razor
@page "/example"
@attribute [Authorize]
@using ResidencyRoll.Shared
@rendermode InteractiveServer

<PageTitle>Example</PageTitle>

<h1>@Title</h1>
<button @onclick="HandleClick">Click Me</button>
```

#### ✅ CORRECT: Component.razor.cs
```csharp
using Microsoft.AspNetCore.Components;

namespace ResidencyRoll.Web.Components.Pages;

public partial class Component
{
    private string Title = "Hello World";
    
    [Inject] private SomeService Service { get; set; } = default!;
    
    protected override async Task OnInitializedAsync()
    {
        // Initialization code
    }
    
    private void HandleClick()
    {
        // Event handler code
    }
}
```

#### ❌ INCORRECT: Do NOT put code in @code blocks
```razor
@page "/example"

<h1>@Title</h1>

@code {
    // DO NOT DO THIS!
    private string Title = "Hello World";
    
    private void HandleClick() { }
}
```

### Key Rules
1. **Never use `@code { }` blocks** in `.razor` files
2. Always create a corresponding `.razor.cs` file for any component with logic
3. Keep the `.razor` file focused on presentation
4. Use `public partial class` in the `.razor.cs` file
5. Match the namespace to the folder structure (e.g., `ResidencyRoll.Web.Components.Pages` for files in `Components/Pages/`)
6. Use proper `using` statements in the `.razor.cs` file for all dependencies

### Benefits
- Better separation of concerns
- Improved code organization and maintainability
- Easier to navigate and test
- Cleaner version control diffs
- Better IDE support for code refactoring
