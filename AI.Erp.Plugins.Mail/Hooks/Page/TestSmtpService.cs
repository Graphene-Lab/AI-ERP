using Microsoft.AspNetCore.Mvc;
using System;
using AI.Erp.Api.Models;
using AI.Erp.Exceptions;
using AI.Erp.Hooks;
using AI.Erp.Plugins.Mail.Api;
using AI.Erp.Plugins.Mail.Services;
using AI.Erp.Utilities;
using AI.Erp.Web.Hooks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Pages.Application;
using AI.Erp.Web.Utils;

namespace AI.Erp.Plugins.Mail.Hooks.Page
{
	[HookAttachment(key: "test_smtp_service")]
	public class TestSmtpService : IRecordDetailsPageHook
	{
		public IActionResult OnGet(RecordDetailsPageModel pageModel)
		{
			return null;
		}

		public IActionResult OnPost(RecordDetailsPageModel pageModel)
		{
			return new SmtpInternalService().TestSmtpServiceOnPost(pageModel);
		}
	}
}
