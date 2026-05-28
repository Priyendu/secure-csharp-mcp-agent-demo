using System;

namespace SecureMcpServer.Security;

public class SecurityAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;

    public SecurityAuditLogger(ILogger<SecurityAuditLogger> logger)
    {
        _logger = logger;
    }

    public void LogAuthSuccess(string endpoint, string user)
    {
        _logger.LogInformation("[AUTH SUCCESS] {Endpoint} accessed by {User} at {Time}", endpoint, user, DateTime.UtcNow);
    }

    public void LogAuthFailure(string endpoint, string reason)
    {
        _logger.LogWarning("[AUTH FAILURE] {Endpoint} failed: {Reason} at {Time}", endpoint, reason, DateTime.UtcNow);
    }

    public void LogToolCall(string tool, string user)
    {
        _logger.LogInformation("[TOOL CALL] {Tool} invoked by {User} at {Time}", tool, user, DateTime.UtcNow);
    }

    public void LogError(string tool, string error)
    {
        _logger.LogError("[TOOL ERROR] {Tool} failed: {Error} at {Time}", tool, error, DateTime.UtcNow);
    }
}