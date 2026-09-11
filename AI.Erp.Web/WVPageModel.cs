using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AI.Erp.Web
{
	public class WVPageModel : PageModel
	{
		public IActionResult OnGet()
		{
			return Page();
		}

	}
}
