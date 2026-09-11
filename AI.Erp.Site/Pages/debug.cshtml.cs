using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Erp.Api;
using AI.Erp.Api.Models;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;
using AI.Erp.Web.Utils;

namespace AI.Erp.Web.Pages
{
	[Authorize]
	public class DebugModel : BaseErpPageModel
	{
		public AppService appService = new AppService();

		public DebugModel([FromServices]ErpRequestContext reqCtx) { ErpRequestContext = reqCtx; }

		public const string ACCOUNT_FIELDS = @"id,
		        email, password";
		public IActionResult OnGet()
        {
			//var list = new PageComponentLibraryService().GetPageComponentsList();

			//ViewBag.LibraryJson = JsonConvert.SerializeObject(new PageComponentLibraryService().GetPageComponentsList());
			//ViewBag.PageNodeListJson = JsonConvert.SerializeObject(new PageService().GetPageNodes(new System.Guid("129937b1-7cbe-42a0-b699-e61bebd28619")));
			var initResult = Init();
            if (initResult != null)
                return initResult;
            var recMan = new RecordManager();
                var query = EntityQuery.QueryEQ("id", Guid.Empty);
                var queryResult = recMan.Find(new EntityQuery("user", ACCOUNT_FIELDS, query));
            ViewData["Result"] = "test";
			return Page();
		}
    }
}
