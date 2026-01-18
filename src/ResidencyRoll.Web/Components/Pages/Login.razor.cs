using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace ResidencyRoll.Web.Components.Pages;

public partial class Login
{
    private bool oidcEnabled = false;

    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        oidcEnabled = Configuration.GetValue<bool>("Authentication:OpenIdConnect:Enabled");
        
        if (oidcEnabled)
        {
            // Automatically redirect to the auth endpoint to trigger OIDC challenge
            Navigation.NavigateTo("/auth/login", forceLoad: true);
        }

        await Task.CompletedTask;
    }
}
