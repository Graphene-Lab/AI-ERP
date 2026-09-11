using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Exceptions;
using AI.Erp.Web;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;

namespace AI.Erp.Plugins.SDK.Pages.Page
{
	public class CreateModel : BaseErpPageModel
	{
		public CreateModel([FromServices]ErpRequestContext reqCtx) { ErpRequestContext = reqCtx; }

		[BindProperty]
		public int Weight { get; set; } = 10;

		[BindProperty]
		public string Label { get; set; } = "";

		[BindProperty(SupportsGet = true)]
		public Guid? PresetEntityId { get; set; } = null;

		//[BindProperty]
		public List<TranslationResource> LabelTranslations { get; set; } = new List<TranslationResource>();

		[BindProperty]
		public string Name { get; set; } = "";

		[BindProperty]
		public string IconClass { get; set; } = "";

		[BindProperty]
		public string Description { get; set; } = "";

		[BindProperty]
		public bool System { get; set; } = false;

		[BindProperty]
		public string Color { get; set; } = "";

		[BindProperty(SupportsGet = true)]
		public PageType Type { get; set; } = PageType.Site;

		[BindProperty(SupportsGet =true)]
		public Guid? AppId { get; set; } = null;

		[BindProperty]
		public Guid? EntityId { get; set; } = null;

		[BindProperty]
		public Guid? AreaId { get; set; } = null;

		[BindProperty]
		public Guid? NodeId { get; set; } = null;

		[BindProperty]
		public bool IsRazorBody { get; set; } = false;

		[BindProperty]
		public string RazorBody { get; set; } = "";

		
		public IActionResult OnGet()
		{
			var initResult = Init();
			if (initResult != null)
				return initResult;

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

			var pageServ = new PageService();
			try
			{
				var pageId = Guid.NewGuid();
				pageServ.CreatePage(pageId, Name, Label, LabelTranslations, IconClass,System,Weight,Type,AppId,EntityId,NodeId,AreaId,false,null,string.Empty);
				return Redirect($"/sdk/objects/page/r/{pageId}/");
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