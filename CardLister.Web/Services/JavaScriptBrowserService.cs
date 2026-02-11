using FlipKit.Core.Services;
using Microsoft.AspNetCore.Http;

namespace FlipKit.Web.Services
{
    /// <summary>
    /// Web implementation of IBrowserService.
    /// Opens URLs by adding a response header that client JavaScript can detect.
    /// In web apps, URL opening is typically done via client-side JavaScript (window.open).
    /// </summary>
    public class JavaScriptBrowserService : IBrowserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public JavaScriptBrowserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Signals the client to open a URL in a new browser tab.
        /// Sets a response header that client JavaScript should check for.
        /// </summary>
        /// <param name="url">The URL to open</param>
        public void OpenUrl(string url)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null && !httpContext.Response.Headers.ContainsKey("X-Open-Url"))
            {
                httpContext.Response.Headers.Append("X-Open-Url", url);
            }
        }
    }
}
