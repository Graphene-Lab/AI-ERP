using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;

namespace AI.Erp.Web.Components
{

	public class SiteMenu : ViewComponent
	{
		protected ErpRequestContext ErpRequestContext { get; set; }

		public SiteMenu([FromServices]ErpRequestContext coreReqCtx)
		{
			ErpRequestContext = coreReqCtx;
		}

		public async Task<IViewComponentResult> InvokeAsync(BaseErpPageModel pageModel)
		{
			ViewBag.SiteMenu = pageModel.SiteMenu;
			return await Task.FromResult<IViewComponentResult>(View("SiteMenu"));
		}
	}
}
