using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorApp1.Services;

/// <summary>
/// Menyediakan AuthenticationState dari HttpContext.User.
/// Prinsipal ditangkap sekali saat scope (request/circuit) dibuat — saat
/// itu HttpContext masih tersedia (termasuk handshake WebSocket circuit),
/// lalu di-cache untuk umur scope tersebut.
/// </summary>
public sealed class ServerAuthStateProvider : AuthenticationStateProvider
{
    private readonly Task<AuthenticationState> _stateTask;

    public ServerAuthStateProvider(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User
                   ?? new ClaimsPrincipal(new ClaimsIdentity());
        _stateTask = Task.FromResult(new AuthenticationState(user));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _stateTask;
}
