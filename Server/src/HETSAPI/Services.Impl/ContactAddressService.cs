/*
 * REST API Documentation for the MOTI Hired Equipment Tracking System (HETS) Application
 *
 * The Hired Equipment Program is for owners/operators who have a dump truck, bulldozer, backhoe or  other piece of equipment they want to hire out to the transportation ministry for day labour and  emergency projects.  The Hired Equipment Program distributes available work to local equipment owners. The program is  based on seniority and is designed to deliver work to registered users fairly and efficiently  through the development of local area call-out lists. 
 *
 * OpenAPI spec version: v1
 * 
 * 
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using HETSAPI.Models;
using HETSAPI.ViewModels;

namespace HETSAPI.Services.Impl
{
    /// <summary>
    /// 
    /// </summary>
    public class ContactAddressService : IContactAddressService
    {
        private readonly DbAppContext _context;

        /// <summary>
        /// Create a service and set the database context
        /// </summary>
        public ContactAddressService(DbAppContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items"></param>
        /// <response code="201">ContactAddress created</response>
        public virtual IActionResult ContactaddressesBulkPostAsync(ContactAddress[] items)
        {
            if (items == null)
            {
                return new BadRequestResult();
            }

            foreach (ContactAddress item in items)
            {                

                // determine if this is an insert or an update            
                bool exists = _context.ContactAddresss.Any(a => a.Id == item.Id);
                if (exists)
                {
                    _context.Update(item);
                }
                else
                {
                    _context.Add(item);
                }
            }
            // Save the changes
            _context.SaveChanges();
            return new NoContentResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <response code="200">OK</response>
        public virtual IActionResult ContactaddressesGetAsync()
        {
            var result = _context.ContactAddresss
                .ToList();
            return new ObjectResult(result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">id of ContactAddress to delete</param>
        /// <response code="200">OK</response>
        /// <response code="404">ContactAddress not found</response>
        public virtual IActionResult ContactaddressesIdDeletePostAsync(int id)
        {
            var exists = _context.ContactAddresss.Any(a => a.Id == id);
            if (exists)
            {
                var item = _context.ContactAddresss.First(a => a.Id == id);
                if (item != null)
                {
                    _context.ContactAddresss.Remove(item);
                    // Save the changes
                    _context.SaveChanges();
                }
                return new ObjectResult(item);
            }
            else
            {
                // record not found
                return new StatusCodeResult(404);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">id of ContactAddress to fetch</param>
        /// <response code="200">OK</response>
        /// <response code="404">ContactAddress not found</response>
        public virtual IActionResult ContactaddressesIdGetAsync(int id)
        {
            var exists = _context.ContactAddresss.Any(a => a.Id == id);
            if (exists)
            {
                var result = _context.ContactAddresss.First(a => a.Id == id);
                return new ObjectResult(result);
            }
            else
            {
                // record not found
                return new StatusCodeResult(404);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">id of ContactAddress to fetch</param>
        /// <param name="item"></param>
        /// <response code="200">OK</response>
        /// <response code="404">ContactAddress not found</response>
        public virtual IActionResult ContactaddressesIdPutAsync(int id, ContactAddress item)
        {
            var exists = _context.ContactAddresss.Any(a => a.Id == id);
            if (exists && id == item.Id)
            {
                _context.ContactAddresss.Update(item);
                // Save the changes
                _context.SaveChanges();
                return new ObjectResult(item);
            }
            else
            {
                // record not found
                return new StatusCodeResult(404);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <response code="201">ContactAddress created</response>
        public virtual IActionResult ContactaddressesPostAsync(ContactAddress item)
        {
            var exists = _context.ContactAddresss.Any(a => a.Id == item.Id);
            if (exists)
            {
                _context.ContactAddresss.Update(item);
            }
            else
            {
                // record not found
                _context.ContactAddresss.Add(item);
            }
            // Save the changes
            _context.SaveChanges();
            return new ObjectResult(item);
        }
    }
}
