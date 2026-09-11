using Microsoft.AspNetCore.Mvc;
using System;
using AI.Erp.Api;
using AI.Erp.Api.Models;
using AI.Erp.Exceptions;
using AI.Erp.Web;
using AI.Erp.Web.Models;

namespace AI.Erp.Plugins.SDK.Pages.Role
{
	public class CreateModel : BaseErpPageModel
	{
		public CreateModel([FromServices]ErpRequestContext reqCtx) { ErpRequestContext = reqCtx; }

		[BindProperty]
		public string Name { get; set; } = "";

		[BindProperty]
		public string Description { get; set; } = "";

		public IActionResult OnGet()
		{
			var initResult = Init();
			if (initResult != null)
				return initResult;

			if (string.IsNullOrWhiteSpace(ReturnUrl)) ReturnUrl = "/sdk/access/role/l/list";

			BeforeRender();
			return Page();
		}

		public IActionResult OnPost()
		{
			if (!ModelState.IsValid) throw new Exception("Antiforgery check failed.");

			var initResult = Init();
			if (initResult != null)
				return initResult;

			if (string.IsNullOrWhiteSpace(ReturnUrl)) ReturnUrl = "/sdk/access/role/l/list";

			try
			{
				ErpRole newRole = new ErpRole();
				newRole.Id = Guid.NewGuid();
				newRole.Name = Name;
				newRole.Description = Description;
				new SecurityManager().SaveRole(newRole);
				BeforeRender();
				return Redirect(ReturnUrl);
			}
			catch (ValidationException ex)
			{
				Validation.Message = ex.Message;
				Validation.Errors = ex.Errors;
				BeforeRender();
				return Page();
			}
		}
	}
}