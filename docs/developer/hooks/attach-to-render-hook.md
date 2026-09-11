<!--{"sort_order":6, "name": "attach-to-render-hook", "label": "Attach to a Render Hook"}-->
# Attach to a Render Hook

You can easily attach any ViewComponent to a render hook by decorating it with the correct attribute and placeholder name.

IMPORTANT: In our application master page there are already render hooks included for: `head-top`, `head-bottom`,`body-toop`, `body-bottom`.

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using AI.Erp.Web.Hooks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;

namespace AI.Erp.Plugins.Next.Components
{
	[RenderHookAttachment("head-top", 10)]
	public class HookMetaHead : ViewComponent
	{
		public async Task<IViewComponentResult> InvokeAsync(BaseErpPageModel pageModel, dynamic model, string placeholder)
		{
			return await Task.FromResult<IViewComponentResult>(View("Next_HookMetaHead"));
		}
	}
}

```
