//using System;
//using System.Security.Claims;
//using System.Security.Principal;
//using AI.Erp.Api.Models;

//namespace AI.Erp.Web.Security
//{
//    public class ErpIdentity : ClaimsIdentity
//    {
//        public override string AuthenticationType { get { return "AIErp"; } }

//        public override bool IsAuthenticated { get { return User != null && !String.IsNullOrWhiteSpace(User.Email); } }

//        public override string Name
//        {
//            get
//            {
//                if (User != null)
//                {
//                    return User.Username;
//                }
//                return null;
//            }
//        }

//        public ErpUser User { get; set; }
//    }
//}
