using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>Simulates sending email: it only writes to the log (the brief says no real email is needed).</summary>
public class SimulatedEmailSender(ILogger<SimulatedEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("[SIMULATED EMAIL] To: {To} | Subject: {Subject}", to, subject);
        return Task.CompletedTask;
    }
}