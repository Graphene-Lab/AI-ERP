"use strict";
(function (window, $) {

	/// Your code goes below
	///////////////////////////////////////////////////////////////////////////////////

	$(function () {

	//	document.addEventListener("WvPbManager_Design_Loaded", function (event) {
	//		if (event && event.payload && event.payload.component_name === "AI.Erp.Web.Components.PcHtmlBlock"){
	//			console.log("AI.Erp.Web.Components.PcBlock Design loaded");
	//		}
	//	});

	//	document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
	//		if (event && event.payload && event.payload.component_name === "AI.Erp.Web.Components.PcHtmlBlock"){
	//			console.log("AI.Erp.Web.Components.PcBlock Design unloaded");
	//		}
	//	});

	//	document.addEventListener("WvPbManager_Options_Loaded", function (event) {
	//		if (event && event.payload && event.payload.component_name === "AI.Erp.Web.Components.PcHtmlBlock") {
	//		}
	//	});

	//	document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
	//		if (event && event.payload && event.payload.component_name === "AI.Erp.Web.Components.PcHtmlBlock"){
	//			console.log("AI.Erp.Web.Components.PcBlock Options unloaded");
	//		}
	//	});

		document.addEventListener("WvPbManager_Node_Moved", function (event) {
			if (event && event.payload && event.payload.component_name === "AI.Erp.Plugins.Project.Components.PcProjectDashboardOverview") {
				console.log("AI.Erp.Plugins.Project.Components.PcProjectDashboardOverview Moved");
			}
		});
	});


	//////////////////////////////////////////////////////////////////////////////////
	/// You code is above

})(window, jQuery);