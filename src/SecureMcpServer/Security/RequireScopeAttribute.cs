using Microsoft.AspNetCore.Authorization;

namespace SecureMcpServer.Security;

public class RequireScopeAttribute : AuthorizeAttribute
{
    public RequireScopeAttribute(params string[] scopes)
    {
        Policy = $"Scope:{string.Join(",", scopes)}";
    }
}