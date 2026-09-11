using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using AI.Erp.Api;
using AI.Erp.Jobs;


namespace AI.Erp.Plugins.Crm
{
	public partial class CrmPlugin : ErpPlugin
	{
		[JsonProperty(PropertyName = "name")]
		public override string Name { get; protected set; } = "crm";

		public override void Initialize(IServiceProvider serviceProvider)
		{
			using (var ctx = SecurityContext.OpenSystemScope())
			{
				ProcessPatches();
			}
		}
	}
}
