using System.Collections.Generic;

namespace AI.Erp.Api.Models
{
	public class SearchResultList : List<SearchResult>
	{
		public int TotalCount { get; set; } = 0;
	}
}
