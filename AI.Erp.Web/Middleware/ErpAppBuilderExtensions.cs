using Microsoft.AspNetCore.Builder;

namespace AI.Erp.Web.Middleware
{
	public static class AppBuilderExtensions
	{
		public static IApplicationBuilder UseErpMiddleware(this IApplicationBuilder app)
		{
			app.UseMiddleware<ErpMiddleware>();
			return app;
		}

		public static IApplicationBuilder UseJwtMiddleware(this IApplicationBuilder app)
		{
			app.UseMiddleware<JwtMiddleware>();
			return app;
		}

		public static IApplicationBuilder UseDebugLogMiddleware(this IApplicationBuilder app)
		{
			app.UseMiddleware<ErpDebugLogMiddleware>();
			return app;
		}

		public static IApplicationBuilder UseErrorHandlingMiddleware(this IApplicationBuilder app)
		{
			app.UseMiddleware<ErpErrorHandlingMiddleware>();
			return app;
		}
	}
}
