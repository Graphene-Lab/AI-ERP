using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI.Erp.Api;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;

namespace AI.Erp.Web.Components
{

	public class SidebarMenu : ViewComponent
    {
		protected ErpRequestContext ErpRequestContext { get; set; }

		public SidebarMenu([FromServices]ErpRequestContext coreReqCtx)
		{
			ErpRequestContext = coreReqCtx;
		}

		public async Task<IViewComponentResult> InvokeAsync(BaseErpPageModel pageModel)
        {
			ViewBag.SidebarMenu = pageModel.SidebarMenu;
			return await Task.FromResult<IViewComponentResult>(View("SidebarMenu"));

		}
    }
}
