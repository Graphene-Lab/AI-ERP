using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using AI.Erp.Api;
using AI.Erp.Web.Services;

namespace AI.Erp.Web.Components
{

	public class UserNavViewComponent : ViewComponent
	{
		protected ErpRequestContext ErpRequestContext { get; set; }

		public UserNavViewComponent([FromServices]ErpRequestContext coreReqCtx)
		{
			ErpRequestContext = coreReqCtx;
		}

		public async Task<IViewComponentResult> InvokeAsync( )
		{
			var pageContext = ErpRequestContext.PageContext;
			ViewBag.CurrentUser = AuthService.GetUser(UserClaimsPrincipal);
			ViewBag.PageId = null;
			if (ErpRequestContext.Page != null) {
				ViewBag.PageId = ErpRequestContext.Page.Id;
			}

			return await Task.FromResult<IViewComponentResult>(View("UserNav.Default"));
		}
	}
}
