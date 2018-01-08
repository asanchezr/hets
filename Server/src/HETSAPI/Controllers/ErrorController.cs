﻿using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using HETSAPI.ViewModels;

namespace HETSAPI.Controllers
{
    /// <summary>
    /// Error Controller for HETS API Application
    /// </summary>
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorController : Controller
    {
        private readonly IHostingEnvironment _env;

        /// <summary>
        /// Error Controller Constructor
        /// </summary>
        /// <param name="env"></param>
        public ErrorController(IHostingEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Default action
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult Index()
        {
            HomeViewModel home = new HomeViewModel
            {
                DevelopmentEnvironment = _env.IsDevelopment()
            };

            if (HttpContext == null) return View(home);

            home.UserId = HttpContext.User.Identity.Name;
            IExceptionHandlerFeature feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            home.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            home.Message = feature?.Error.Message;

            return View(home);
        }
    }
}