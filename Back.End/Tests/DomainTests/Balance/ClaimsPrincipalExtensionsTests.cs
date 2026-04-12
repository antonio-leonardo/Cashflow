using System.Security.Claims;
using Cashflow.Shared.Identity.Abstractions;

namespace Balance.Domain.Tests;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void HasScope_ShouldMatchScopeAndScpClaims()
    {
        var principal = CreatePrincipal(
            new Claim("scope", "transactions.read transactions.write"),
            new Claim("scp", "balance.read"));

        Assert.True(principal.HasScope("transactions.write"));
        Assert.True(principal.HasScope("balance.read"));
        Assert.False(principal.HasScope("unknown.scope"));
    }

    [Fact]
    public void HasRole_ShouldMatchRoleClaimsAndRealmAccessRoles()
    {
        var principal = CreatePrincipal(
            new Claim("role", "legacy-admin"),
            new Claim("roles", "[\"transactions.writer\",\"report.reader\"]"),
            new Claim("realm_access", "{\"roles\":[\"audit.viewer\"]}"));

        Assert.True(principal.HasRole("legacy-admin"));
        Assert.True(principal.HasRole("transactions.writer"));
        Assert.True(principal.HasRole("audit.viewer"));
        Assert.False(principal.HasRole("missing-role"));
    }

    [Fact]
    public void HasRole_ShouldMatchResourceAccessRoles()
    {
        var principal = CreatePrincipal(
            new Claim(
                "resource_access",
                "{\"cashflow-api\":{\"roles\":[\"transactions.writer\",\"balance.reader\"]},\"cashflow-worker\":{\"roles\":[\"report.writer\"]}}"));

        Assert.True(principal.HasRole("transactions.writer"));
        Assert.True(principal.HasRole("balance.reader"));
        Assert.True(principal.HasRole("report.writer"));
        Assert.False(principal.HasRole("missing-role"));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }
}
