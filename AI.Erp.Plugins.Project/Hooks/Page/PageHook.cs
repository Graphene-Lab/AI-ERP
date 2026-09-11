using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Eql;
using AI.Erp.Hooks;
using AI.Erp.Plugins.Project.Services;
using AI.Erp.Web.Hooks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Plugins.Project.Hooks.Page
{
	[HookAttachment]
	public class PageHook : IPageHook
	{
		private Guid CREATE_TASK_PAGE_ID = new Guid("68100014-1fd7-456c-9b26-27aa9f858287");

		public IActionResult OnGet(BaseErpPageModel pageModel)
		{
			var page = pageModel.ErpRequestContext.Page;

			// Init task create form
			if (page != null && page.Id == CREATE_TASK_PAGE_ID)
			{
				EntityRecord record = (EntityRecord)pageModel.DataModel.GetProperty("Record");

				var resultRecord = new TaskService().GetPageHookLogic(pageModel, record);

				pageModel.DataModel.SetRecord(resultRecord);
			}

			//User menu items
			new RenderService().UserMenuItemsManagement(pageModel);

			return null;
		}

		public IActionResult OnPost(BaseErpPageModel pageModel)
		{
			//User menu items
			new RenderService().UserMenuItemsManagement(pageModel);

			return null;
		}

	}

}
