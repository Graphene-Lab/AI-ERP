using AI.Erp.Api.Models;

namespace AI.Erp.Recurrence
{
	public enum RecurrenceRepeatMonthType
	{
		[SelectOption(Label = "by day")]
		ByDate = 0,
		[SelectOption(Label = "by week day")]
		ByWeekDay = 1
	}
}
