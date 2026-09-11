using System;
using System.ComponentModel;

namespace AI.Erp.Api.Models
{
    public enum QuerySortType
    {
		[SelectOption(Label = "asc")]
		Ascending,
		[SelectOption(Label = "desc")]
		Descending
    }
}