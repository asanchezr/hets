﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using HETSAPI.Authorization;
using HETSAPI.Helpers;
using HETSAPI.Models;
using HETSAPI.ViewModels;
using Microsoft.AspNetCore.Hosting;

namespace HETSAPI.Controllers
{
    /// <summary>
    /// This controller is used to handle importing data from the previous system (BC Bid)
    /// </summary>
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class ImportController : Controller
    {
        private readonly HttpContext _context;
        private readonly IHostingEnvironment _env;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Import Controller Constructor
        /// </summary>
        /// <param name="httpContextAccessor"></param>
        /// <param name="env"></param>
        /// <param name="configuration"></param>
        public ImportController(IHttpContextAccessor httpContextAccessor, IHostingEnvironment env, IConfiguration configuration)
        {
            _context = httpContextAccessor.HttpContext;
            _env = env;
            _configuration = configuration;
        }

        /// <summary>
        /// Default action
        /// </summary>
        /// <returns></returns>
        [Route("/api/Import")]
        [RequiresPermission(Permission.ImportData)]
        public IActionResult Index()
        {
            string path = _context.Request.Path.ToString().ToLower();

            if (!path.EndsWith(@"/"))
            {
                path = path + @"/";
            }

            path = path.Replace("/import", "");

            // this will need to be removed once the Reverse Proxy is fixed
            if (!_env.IsDevelopment())
            {
                path = "/hets" + path;
            }

            HomeViewModel home = new HomeViewModel
            {
                UserId = HttpContext.User.Identity.Name,
                DevelopmentEnvironment = _env.IsDevelopment(),
                Action = path + "UploadPost"
            };

            return View(home);
        }

        /// <summary>
        /// Receives uploaded files
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>      
        [Route("/api/UploadPost")]
        [RequiresPermission(Permission.ImportData)]
        public IActionResult UploadPost(IList<IFormFile> files)
        {
            string path = _context.Request.Path.ToString().ToLower();

            if (!path.EndsWith(@"/"))
            {
                path = path + @"/";
            }

            path = path.Replace("/uploadpost", "");

            // this will need to be removed once the Reverse Proxy is fixed
            if (!_env.IsDevelopment())
            {
                path = "/hets" + path;
            }

            // get the upload path from the app configuration
            string uploadPath = _configuration["UploadPath"];

            HomeViewModel home = UploadHelper.UploadFiles(files, uploadPath);

            home.UserId = HttpContext.User.Identity.Name;
            home.DevelopmentEnvironment = _env.IsDevelopment();
            home.Action = path + "UploadPost";

            return View("Index", home);
        }
    }
}
