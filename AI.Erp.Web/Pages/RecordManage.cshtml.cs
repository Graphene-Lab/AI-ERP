using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Erp.Api;
using AI.Erp.Api.Models;
using AI.Erp.Api.Models.AutoMapper;
using AI.Erp.Diagnostics;
using AI.Erp.Exceptions;
using AI.Erp.Hooks;
using AI.Erp.Web.Hooks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;

namespace AI.Erp.Web.Pages.Application
{
	public class RecordManagePageModel : BaseErpPageModel
	{
		public RecordManagePageModel([FromServices]ErpRequestContext reqCtx) { ErpRequestContext = reqCtx; }

		public IActionResult OnGet()
		{
			try
			{
				var initResult = Init();
				if (initResult != null) return initResult;
				if (ErpRequestContext.Page == null) return NotFound();
				if (!RecordsExists()) return NotFound();
				if (PageName != ErpRequestContext.Page.Name)
				{
					var queryString = HttpContext.Request.QueryString.ToString();
					return Redirect($"/{ErpRequestContext.App.Name}/{ErpRequestContext.SitemapArea.Name}/{ErpRequestContext.SitemapNode.Name}/m/{ErpRequestContext.RecordId}/{ErpRequestContext.Page.Name}{queryString}");
				}

				var globalHookInstances = HookManager.GetHookedInstances<IPageHook>(HookKey);
				foreach (IPageHook inst in globalHookInstances)
				{
					var result = inst.OnGet(this);
					if (result != null) return result;
				}

				BeforeRender();
				return Page();
			}
			catch (Exception ex)
			{
				new Log().Create(LogType.Error, "RecordManagePageModel Error on GET", ex);
				Validation.Message = ex.Message;
				BeforeRender();
				return Page();
			}
		}

		public IActionResult OnPost()
		{
			try
			{
				if (!ModelState.IsValid) throw new Exception("Antiforgery check failed.");
				var initResult = Init();
				if (initResult != null) return initResult;
				if (ErpRequestContext.Page == null) return NotFound();
				if (!RecordsExists()) return NotFound();
				if (PageName != ErpRequestContext.Page.Name)
					return Redirect($"/{ErpRequestContext.App.Name}/{ErpRequestContext.SitemapArea.Name}/{ErpRequestContext.SitemapNode.Name}/m/{ErpRequestContext.RecordId}/{ErpRequestContext.Page.Name}");

				//Standard Page functionality
				var PostObject = new PageService().ConvertFormPostToEntityRecord(PageContext.HttpContext, entity: ErpRequestContext.Entity, recordId: RecordId);
				DataModel.SetRecord(PostObject);

				var globalHookInstances = HookManager.GetHookedInstances<IPageHook>(HookKey);
				foreach (IPageHook inst in globalHookInstances)
				{
					var result = inst.OnPost(this);
					if (result != null) return result;
				}


				if (!PostObject.Properties.ContainsKey("id"))
					PostObject["id"] = RecordId.Value;

				var hookInstances = HookManager.GetHookedInstances<IRecordManagePageHook>(HookKey);

				//pre manage hooks
				foreach (IRecordManagePageHook inst in hookInstances)
				{
					List<ValidationError> errors = new List<ValidationError>();
					var result = inst.OnPreManageRecord(PostObject, ErpRequestContext.Entity, this, errors);
					if (result != null) return result;
					if (errors.Any())
					{
						Validation.Errors.AddRange(errors);
						BeforeRender();
						return Page();
					}
				}
				//record submission validates required fields and auto number - these fields are validated in recordmanager
				ValidateRecordSubmission(PostObject, ErpRequestContext.Entity, Validation);
				if (Validation.Errors.Any())
				{
					BeforeRender();
					return Page();
				}

				var updateResponse = new RecordManager().UpdateRecord(ErpRequestContext.Entity.MapTo<Entity>(), PostObject);
				if (!updateResponse.Success)
				{
					Validation.Message = updateResponse.Message;
					foreach (var error in updateResponse.Errors)
						Validation.Errors.Add(new ValidationError(error.Key, error.Message));

					ErpRequestContext.PageContext = PageContext;
					BeforeRender();
					return Page();
				}

				//post manage hook
				foreach (IRecordManagePageHook inst in hookInstances)
				{
					var result = inst.OnPostManageRecord(PostObject, ErpRequestContext.Entity, this);
					if (result != null) return result;
				}

				if (string.IsNullOrWhiteSpace(ReturnUrl))
					return Redirect($"/{ErpRequestContext.App.Name}/{ErpRequestContext.SitemapArea.Name}/{ErpRequestContext.SitemapNode.Name}/r/{updateResponse.Object.Data[0]["id"]}");
				else
					return Redirect(ReturnUrl);
			}
			catch (ValidationException valEx)
			{
				Validation.Message = valEx.Message;
				Validation.Errors.AddRange(valEx.Errors);
				BeforeRender();
				return Page();
			}
			catch (Exception ex)
			{
				new Log().Create(LogType.Error, "RecordManagePageModel Error on POST", ex);
				Validation.Message = ex.Message;
				BeforeRender();
				return Page();
			}
		}
	}
}