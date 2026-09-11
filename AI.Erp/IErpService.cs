using System;
using System.Collections.Generic;
using AI.Erp.Jobs;

namespace AI.Erp
{
    public interface IErpService
    {
		List<ErpPlugin> Plugins { get; set; }

		void InitializeSystemEntities();
		void InitializeBackgroundJobs(List<JobType> additionalJobTypes = null);
		void StartBackgroundJobProcess();
		void InitializePlugins(IServiceProvider app);
		void SetAutoMapperConfiguration();
	}
}