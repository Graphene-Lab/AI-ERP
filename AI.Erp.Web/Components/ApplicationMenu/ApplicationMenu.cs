using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;

namespace AI.Erp.Web.Components
{

	public class ApplicationMenu : ViewComponent
	{
		protected ErpRequestContext ErpRequestContext { get; set; }

		public ApplicationMenu([FromServices]ErpRequestContext coreReqCtx)
		{
			ErpRequestContext = coreReqCtx;
		}

		public async Task<IViewComponentResult> InvokeAsync(BaseErpPageModel pageModel)
		{
			 var areaList = pageModel.ApplicationMenu;
			foreach (var area in areaList)
			{
				area.Nodes = new RenderService().ConvertListToTree(area.Nodes,new List<MenuItem>(),null);
			}

			 ViewBag.ApplicationMenu = areaList;
			return await Task.FromResult<IViewComponentResult>(View("ApplicationMenu"));
		}
	}
}
