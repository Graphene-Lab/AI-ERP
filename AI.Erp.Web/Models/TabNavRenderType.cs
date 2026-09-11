using System.ComponentModel;
using AI.Erp.Api.Models;

namespace AI.Erp.Web.Models
{
	public enum TabNavRenderType
	{
		[SelectOption(Label = "Tabs")]
		TABS = 1,
		[SelectOption(Label = "Pills")]
		PILLS = 2
	}
}
