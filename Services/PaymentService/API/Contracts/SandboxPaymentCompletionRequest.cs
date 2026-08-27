namespace PaymentService.API.Contracts;

// Development/Portfolio simulator input only. No payment instrument fields are accepted.
public sealed record SandboxPaymentCompletionRequest(string Outcome);
