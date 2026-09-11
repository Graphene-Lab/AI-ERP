using AI.Erp.Api;
using AI.Erp.Jobs;
using AI.Erp.Plugins.Mail.Services;

namespace AI.Erp.Plugins.Mail.Jobs
{
	[Job("9b301dca-6c81-40dd-887c-efd31c23bd77", "Process SMTP queue", true, JobPriority.Low)]
	public class ProcessSmtpQueueJob : ErpJob
	{
		public override void Execute(JobContext context)
		{
			using (SecurityContext.OpenSystemScope())
			{
				new SmtpInternalService().ProcessSmtpQueue();
			}
		}
	}
}
