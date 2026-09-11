using System;
using Newtonsoft.Json;
using AI.Erp;
using AI.Erp.Api;

namespace AI.Erp.Plugins.MicrosoftCDM
{
	public partial class MicrosoftCDMPlugin : ErpPlugin
	{

		[JsonProperty(PropertyName = "name")]
		public override string Name { get; protected set; } = "MicrosoftCDMPlugin";

		public override void Initialize(IServiceProvider serviceProvider)
		{
			using (var ctx = SecurityContext.OpenSystemScope())
			{
				ProcessPatches();
			}
		}

	}
}
