// "Một sản phẩm từ phòng sharepoint. SIMAX-CôngVM"

using Microsoft.AspNetCore.Mvc;
using Service.Shared.Commons.Interfaces.Extentions;
using Service.Shared.Commons.Model.PHANQUYEN;

namespace Service.Core.Api.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    public class BaseController : ControllerBase
    {

        /// <summary>
        /// 
        /// </summary>
        protected CurrentUserDto CurrentUser { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestContext"></param>
        public BaseController(IRequestContext requestContext)
        {
            CurrentUser = requestContext.CurrentUser;
        }

    }

}
