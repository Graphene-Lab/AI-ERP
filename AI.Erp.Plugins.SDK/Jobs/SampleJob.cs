using System.Threading;
using AI.Erp.Diagnostics;
using AI.Erp.Jobs;

namespace AI.Erp.Plugins.SDK.Jobs
{
	[Job("559c557a-0fd3-4235-b061-117197154ca5", "Sample job", true, JobPriority.Medium)]
	public class SampleJob : ErpJob
	{
		public override void Execute(JobContext context)
		{
			var log = new Log();
			log.Create(LogType.Info, "Sample job","Execute Sample Job started", "");
			Thread.Sleep(5000);
			log.Create(LogType.Info, "Sample job", "Execute Sample Job completed.", "");
		}
	}
}
