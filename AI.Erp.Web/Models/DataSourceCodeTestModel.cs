using Newtonsoft.Json;
using System.Collections.Generic;
using AI.Erp.Api.Models;
namespace AI.Erp.Web.Models
{
	public class DataSourceCodeTestModel
	{
		[JsonProperty(PropertyName = "csCode")]
		public string CsCode { get; set; }
	}
}
