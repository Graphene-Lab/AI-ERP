using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using AI.Erp.Exceptions;
using AI.Erp.Plugins.SDK.Utils;
using AI.Erp.Web;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;

namespace AI.Erp.Plugins.SDK.Pages.Page
{
	public class ManageCustomModel : BaseErpPageModel
	{
		public ManageCustomModel([FromServices]ErpRequestContext reqCtx) { ErpRequestContext = reqCtx; }

		public ErpPage ErpPage { get; set; }

		[BindProperty]
		public bool IsRazorBody { get; set; } = false;

		[BindProperty]
		public string RazorBody { get; set; } = "";

		public List<string> HeaderToolbar { get; private set; } = new List<string>();

		private void InitPage()
		{
			#region << Init Page >>
			var pageServ = new PageService();
			ErpPage = pageServ.GetPage(RecordId ?? Guid.Empty);
			if (ErpPage != null && PageContext.HttpContext.Request.Method == "GET")
			{
				IsRazorBody = ErpPage.IsRazorBody;
				RazorBody = ErpPage.RazorBody;
			}


			#endregion

			HeaderToolbar.AddRange(AdminPageUtils.GetPageAdminSubNav(ErpPage, "manage-custom"));
		}

		public IActionResult OnGet()
		{
			var initResult = Init();
			if (initResult != null)
				return initResult;

			InitPage();

			if (ErpPage == null)
			{
				return NotFound();
			}

			ErpRequestContext.PageContext = PageContext;

			BeforeRender();
			return Page();
		}

		public IActionResult OnPost()
		{
			if (!ModelState.IsValid) throw new Exception("Antiforgery check failed.");

			var initResult = Init();
			if (initResult != null)
				return initResult;

			InitPage();

			if (ErpPage == null)
			{
				return NotFound();
			}

			var pageServ = new PageService();
			try
			{
				pageServ.UpdatePage(ErpPage.Id, ErpPage.Name, ErpPage.Label, ErpPage.LabelTranslations, ErpPage.IconClass, ErpPage.System, ErpPage.Weight, ErpPage.Type, ErpPage.AppId, ErpPage.EntityId, ErpPage.NodeId, ErpPage.AreaId, IsRazorBody, RazorBody, ErpPage.Layout);
				if (!String.IsNullOrWhiteSpace(ReturnUrl))
				{
					return Redirect(ReturnUrl);
				}
				else
				{
					return Redirect($"/sdk/objects/application/r/{ErpPage.Id}/");
				}
			}
			catch (ValidationException ex)
			{
				Validation.Message = ex.Message;
				Validation.Errors = ex.Errors;
			}

			ErpRequestContext.PageContext = PageContext;

			BeforeRender();
			return Page();
		}
	}
}