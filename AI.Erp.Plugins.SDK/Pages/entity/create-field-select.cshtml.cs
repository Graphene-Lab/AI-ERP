using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using AI.Erp.Api;
using AI.Erp.Api.Models;
using AI.Erp.Plugins.SDK.Utils;
using AI.Erp.Web;
using AI.Erp.Web.Models;

namespace AI.Erp.Plugins.SDK.Pages.ErpEntity
{
	public class CreateFieldSelectModel : BaseErpPageModel
	{
		public CreateFieldSelectModel([FromServices]ErpRequestContext reqCtx) { ErpRequestContext = reqCtx; }
		
		public Entity ErpEntity { get; set; }

		public string CreateFieldUrl { get; set; } = "";

		public List<EntityRecord> FieldCards { get; set; } = new List<EntityRecord>();

		public List<string> HeaderActions { get; private set; } = new List<string>();

		public List<string> HeaderToolbar { get; private set; } = new List<string>();

		public void InitPage()
		{
			var entMan = new EntityManager();
			ErpEntity = entMan.ReadEntity(ParentRecordId ?? Guid.Empty).Object;

			CreateFieldUrl = $"/sdk/objects/entity/r/{ParentRecordId}/rl/fields/c/?FieldTypeId=";

			FieldCards = AdminPageUtils.GetFieldCards();

		}
		public IActionResult OnGet()
        {
			var initResult = Init();
			if (initResult != null)
				return initResult;

			InitPage();

			if (ErpEntity == null)
			{
				return NotFound();
			}
			HeaderToolbar.AddRange(AdminPageUtils.GetEntityAdminSubNav(ErpEntity, "fields"));

			ErpRequestContext.PageContext = PageContext;

			BeforeRender();
			return Page();
		}

	}
}