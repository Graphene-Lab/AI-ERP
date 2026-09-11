using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using AI.Erp.Database;
using AI.Erp.Api;
using System;
using AI.Erp.Api.Models;
using AI.Erp.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;

namespace AI.Erp.Web.Middleware
{
	public class ErpMiddleware
	{
		RequestDelegate next;

		public ErpMiddleware(RequestDelegate next)
		{
			this.next = next;
		}

		public async Task Invoke(HttpContext context)
		{
			var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
			if (syncIOFeature != null)
				syncIOFeature.AllowSynchronousIO = true;

			IDisposable dbCtx = DbContext.CreateContext(ErpSettings.ConnectionString);
			IDisposable secCtx = null;

			ErpUser user = AuthService.GetUser(context.User);
			if (user != null)
			{
				secCtx = SecurityContext.OpenScope(user);
			}
			else
			{
				if (context.User.Identity.IsAuthenticated)
				{
					await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
				}
			}

			await next(context);
			await Task.Run(() =>
			{
				if (dbCtx != null)
					dbCtx.Dispose();
				if (secCtx != null)
					secCtx.Dispose();
			});
		}
	}
}