using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AI.Erp.Web.Models;

namespace AI.Erp.Web.Components
{

	public class SearchNavViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync( )
        {
			return await Task.FromResult<IViewComponentResult>(View("SearchNav.Default"));
        }
    }
}
