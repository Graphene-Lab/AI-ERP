using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Erp.Api;
using AI.Erp.Api.Models;
using AI.Erp.Database;
using AI.Erp.Eql;
using AI.Erp.Hooks;
using AI.Erp.Plugins.Project.Services;
using AI.Erp.Web.Hooks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Plugins.Project.Hooks.Page
{
	[HookAttachment(key: "TimeTrackCreateLog")]
	public class _TimeTrackCreateLog : IApplicationNodePageHook
	{

		public IActionResult OnGet(ApplicationNodePageModel pageModel)
		{
			return null;
		}

		public IActionResult OnPost(ApplicationNodePageModel pageModel)
		{
			return new TimeLogService().PostApplicationNodePageHookLogic(pageModel);
		}
	}

}
