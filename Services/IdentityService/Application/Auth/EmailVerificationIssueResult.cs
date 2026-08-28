namespace IdentityService.Application.Auth;

public enum EmailVerificationIssueResult
{
    Issued,
    AlreadyVerified,
    RateLimited,
    NotEligible
}
