namespace SecureMcpServer.Models;

public record ReleaseStatus(string Component, string Version, string Status, DateTime LastUpdated);
public record Dependency(string Name, string Version, string Status);
public record Vulnerability(string Id, string Severity, string Description);