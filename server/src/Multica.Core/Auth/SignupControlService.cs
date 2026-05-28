using Microsoft.Extensions.Configuration;

namespace Multica.Core.Auth;

/// <summary>
/// Service for validating signup controls (AllowSignup, AllowedEmails, AllowedEmailDomains).
/// Compatible with Go's signup control implementation.
/// </summary>
public class SignupControlService
{
    private readonly bool _allowSignup;
    private readonly HashSet<string> _allowedEmails;
    private readonly HashSet<string> _allowedEmailDomains;

    public SignupControlService(IConfiguration config)
    {
        _allowSignup = bool.Parse(config["Auth:AllowSignup"] ?? "true");

        var emails = config.GetSection("Auth:AllowedEmails").Get<string[]>() ?? [];
        _allowedEmails = new HashSet<string>(emails, StringComparer.OrdinalIgnoreCase);

        var domains = config.GetSection("Auth:AllowedEmailDomains").Get<string[]>() ?? [];
        _allowedEmailDomains = new HashSet<string>(domains, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a new user with the given email is allowed to sign up.
    /// Existing users can always log in regardless of signup controls.
    /// </summary>
    public bool IsSignupAllowed(string email)
    {
        // If signup is disabled, reject new users
        if (!_allowSignup)
            return false;

        // If allowed emails list is non-empty, email must be in list
        if (_allowedEmails.Count > 0 && !_allowedEmails.Contains(email))
            return false;

        // If allowed domains list is non-empty, email domain must match
        if (_allowedEmailDomains.Count > 0)
        {
            var domain = email.Split('@').LastOrDefault();
            if (domain is null || !_allowedEmailDomains.Contains(domain))
                return false;
        }

        return true;
    }
}
