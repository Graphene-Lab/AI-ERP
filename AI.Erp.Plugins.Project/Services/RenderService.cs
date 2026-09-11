using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Erp.Api.Models;
using AI.Erp.Eql;
using AI.Erp.Exceptions;
using AI.Erp.Web.Models;

namespace AI.Erp.Plugins.Project.Services
{
	public class RenderService : BaseService
	{
		public void UserMenuItemsManagement(BaseErpPageModel pageModel) {
			if (pageModel.AppName == "projects")
			{
				var createTask = new MenuItem()
				{
					IsHtml = true,
					Content = "<div class='menu-nav-wrapper'><div class='menu-nav'><a href='/projects/tasks/tasks/c/create'><i class='fa fa-plus'></i> Create Task</a></div></div>",
					isDropdownRight = false,
					RenderWrapper = false,
					SortOrder = 1
				};
				pageModel.AddUserMenu(createTask);
			}
		}
	}
}
