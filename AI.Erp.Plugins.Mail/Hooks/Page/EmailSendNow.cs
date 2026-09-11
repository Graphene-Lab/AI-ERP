using Microsoft.AspNetCore.Mvc;
using System;
using AI.Erp.Hooks;
using AI.Erp.Plugins.Mail.Api;
using AI.Erp.Plugins.Mail.Services;
using AI.Erp.Web.Hooks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Pages.Application;
using AI.Erp.Web.Utils;

namespace AI.Erp.Plugins.Mail.Hooks.Page
{
	[HookAttachment(key: "email_send_now")]
	public class EmailSendNow : IRecordDetailsPageHook
	{
		public IActionResult OnPost(RecordDetailsPageModel pageModel)
		{
			return new SmtpInternalService().EmailSendNowOnPost(pageModel);
		}
	}
}
