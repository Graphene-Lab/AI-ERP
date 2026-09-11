using Newtonsoft.Json;
using System;
using AI.Erp.Api.Models;

namespace AI.Erp.Recurrence
{
	public enum RecurrenceChangeType {
		[SelectOption(Label = "only this")]
		OnlyThis = 0,
		[SelectOption(Label = "this and all that follow")]
		ThisAndAllFollowingThis = 1,
		[SelectOption(Label = "all from this template")]
		All = 2
	}
}
