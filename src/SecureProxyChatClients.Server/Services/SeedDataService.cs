using Microsoft.AspNetCore.Identity;

namespace SecureProxyChatClients.Server.Services;

public class SeedDataService(
    UserManager<IdentityUser> userManager,
    IConfiguration configuration,
    ILogger<SeedDataService> logger)
{
    public async Task SeedAsync()
    {
        var email = configuration.GetValue<string>("SeedUser:Email") ?? "test@test.com";
        var password = configuration.GetValue<string>("SeedUser:Password") ?? "Test123!";

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            logger.LogInformation("Seed user {Email} already exists", email);
            return;
        }

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            logger.LogInformation("Seed user {Email} created", email);
        }
        else
        {
            logger.LogError("Failed to create seed user {Email}: {Errors}",
                email, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
