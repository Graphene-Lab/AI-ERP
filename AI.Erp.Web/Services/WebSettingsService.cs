using AI.Erp.Web.Models;

namespace AI.Erp.Web.Services
{
	public class WebSettingsService : BaseService
	{
		WebSettings coresettings = new WebSettings();

		public WebSettings Get()
		{
			return coresettings; 
		}
	}
}
