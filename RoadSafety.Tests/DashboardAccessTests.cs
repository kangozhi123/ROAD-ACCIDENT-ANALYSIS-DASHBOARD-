using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RoadSafety.Tests;

public class DashboardAccessTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public DashboardAccessTests(TestWebAppFactory factory) => _factory = factory;

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// The auth challenge emits an absolute Location while RedirectToPage emits
    /// a relative one, and Uri.AbsolutePath throws on relative URIs.
    /// </summary>
    private static string PathOf(Uri location) =>
        location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];

    [Fact]
    public async Task An_anonymous_visitor_is_redirected_away_from_the_dashboard()
    {
        var response = await CreateClient().GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // The challenge sends the visitor to the login page (LoginPath = "/")
        // and remembers where they were trying to go.
        var location = response.Headers.Location!;
        Assert.Equal("/", PathOf(location));
        Assert.Contains("ReturnUrl=%2FDashboard", location.OriginalString);
    }

    [Fact]
    public async Task The_login_page_is_reachable_anonymously()
    {
        var response = await CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Officer sign in", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_get_request_to_logout_does_not_sign_anyone_out()
    {
        var response = await CreateClient().GetAsync("/Logout");

        // Redirects to the login page rather than performing the sign-out,
        // so a cross-site GET cannot log a user out.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", PathOf(response.Headers.Location!));
    }
}
