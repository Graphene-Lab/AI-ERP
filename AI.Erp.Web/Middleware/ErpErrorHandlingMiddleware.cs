using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using AI.Erp.Api;
using AI.Erp.Database;
using AI.Erp.Web.Services;

namespace AI.Erp.Web.Middleware
{
    public class ErpErrorHandlingMiddleware
    {
        private readonly RequestDelegate next;

        public ErpErrorHandlingMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                await LogError(context, ex);

                throw;
            }
        }

        private static async Task LogError(HttpContext context, Exception ex)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        using (var dbCtx = DbContext.CreateContext(ErpSettings.ConnectionString))
                        {
                            using (SecurityContext.OpenSystemScope())
                            {
                                HttpRequest request = null;
                                if (context != null)
                                    request = context.Request;

                                //ignore file access errors
                                if (ex != null && ex.Message.Contains("The process cannot access the file"))
                                    return;
                                
                               new LogService().Create(Diagnostics.LogType.Error, "Global", ex, request);
                            }
                        }
                    }
                    catch
                    {

                    }
                });
            }
            catch
            {

            }
        }
    }
}
