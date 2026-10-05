using Harmony.Identity.Infrastructure.IdentityServer.Yarp;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// The rules every sign-in page is sent with: no script runs (there is none), styles only from this host, the forms post
/// only here and their redirects end only at the web app (the BFF), no other site may frame the page, the browser keeps
/// no copy, and the address never leaks to another site in a Referer header.
/// </summary>
public sealed class SignInPageHeadersAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is PageResult)
        {
            var bff = context.HttpContext.RequestServices.GetRequiredService<IOptions<WebBffSettings>>().Value;
            var headers = context.HttpContext.Response.Headers;

            headers.ContentSecurityPolicy =
                "default-src 'none'; style-src 'self'; img-src 'self'; " +
                $"form-action 'self' {string.Join(' ', bff.Origins)}; frame-ancestors 'none'; base-uri 'none'";
            headers.XFrameOptions = "DENY";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.CacheControl = "no-store";
        }

        base.OnResultExecuting(context);
    }
}
