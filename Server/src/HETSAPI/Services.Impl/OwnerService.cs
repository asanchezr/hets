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
    public class OwnerService : IOwnerService
    {
        private readonly DbAppContext _context;

        /// <summary>
        /// Create a service and set the database context
        /// </summary>
        public OwnerService(DbAppContext context)
        {
            _context = context;
        }

        private void AdjustRecord(Owner item)
        {
            // Adjust the record to allow it to be updated / inserted
            if (item.LocalArea != null)
            {
                int localarea_id = item.LocalArea.Id;
                bool localarea_exists = _context.LocalAreas.Any(a => a.Id == localarea_id);
                if (localarea_exists)
                {
                    LocalArea localarea = _context.LocalAreas.First(a => a.Id == localarea_id);
                    item.LocalArea = localarea;
                }
                else
                {
                    item.LocalArea = null;
                }
            }

            // Adjust the owner contacts.            
            if (item.Contacts != null)
            {
                for (int i = 0; i < item.Contacts.Count; i++)
                {
                    Contact contact = item.Contacts[i];
                    if (contact != null)
                    {
                        int contact_id = contact.Id;
                        bool contact_exists = _context.Contacts.Any(a => a.Id == contact_id);
                        if (contact_exists)
                        {
                            contact = _context.Contacts.First(a => a.Id == contact_id);
                            item.Contacts[i] = contact;
                        }
                        else
                        {
                            item.Contacts[i] = null;
                        }
                    }
                }
            }

            if (item.PrimaryContact != null)
            {
                int primaryContact_id = item.PrimaryContact.Id;
                bool primaryContact_exists = _context.Contacts.Any(a => a.Id == primaryContact_id);
                if (primaryContact_exists)
                {
                    item.PrimaryContact = _context.Contacts.First(a => a.Id == primaryContact_id);                         
                }
                else
                {
                    item.PrimaryContact = null;
                }               
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items"></param>
        /// <response code="201">Owner created</response>
        public virtual IActionResult OwnersBulkPostAsync(Owner[] items)
        {
            if (items == null)
            {
                return new BadRequestResult();
            }
            foreach (Owner item in items)
            {
                AdjustRecord(item);

                // determine if this is an insert or an update            
                bool exists = _context.Owners.Any(a => a.Id == item.Id);
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
        public virtual IActionResult OwnersGetAsync()
        {
            var result = _context.Owners
        .Include(x => x.LocalArea.ServiceArea.District.Region)        
        .ToList();
            return new ObjectResult(result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">id of Owner to fetch Contacts for</param>
        /// <response code="200">OK</response>
        public virtual IActionResult OwnersIdContactsGetAsync(int id)
        {
            var exists = _context.Owners.Any(a => a.Id == id);
            if (exists)
            {
                Owner owner = _context.Owners
                    .Include(x => x.LocalArea.ServiceArea.District.Region)
                    .Include(x => x.EquipmentList)
                    .ThenInclude(y => y.EquipmentType)
                    .Include(x => x.Notes)
                    .Include(x => x.Attachments)
                    .Include(x => x.History)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Addresses)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Phones)
                    .First(x => x.Id == id);
                                                
                return new ObjectResult(owner.Contacts);
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
        /// <remarks>Replaces an Owner&#39;s Contacts</remarks>
        /// <param name="id">id of Owner to replace Contacts for</param>
        /// <param name="items">Replacement Owner contacts.</param>
        /// <response code="200">OK</response>
        public virtual IActionResult OwnersIdContactsPutAsync(int id, Contact[] items)
        {
            var exists = _context.Owners.Any(a => a.Id == id);
            if (exists && items != null)
            {
                Owner owner = _context.Owners
                    .Include(x => x.LocalArea.ServiceArea.District.Region)
                    .Include(x => x.EquipmentList)
                    .ThenInclude(y => y.EquipmentType)
                    .Include(x => x.Notes)
                    .Include(x => x.Attachments)
                    .Include(x => x.History)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Addresses)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Phones)
                    .First(x => x.Id == id);

                // adjust the incoming list.

                for (int i = 0; i < items.Count(); i++)
                {
                    Contact item = items[i];
                    if (item != null)
                    {
                        bool contact_exists = _context.Contacts.Any(x => x.Id == item.Id);
                        if (contact_exists)
                        {
                            items[i] = _context.Contacts
                                .Include(x => x.Addresses)
                                .Include(x => x.Phones)
                                .First(x => x.Id == item.Id);
                        }
                        else
                        {
                            _context.Add(item);
                            items[i] = item;
                        }
                    }                    
                }

                // remove contacts that are no longer attached.

                foreach (Contact contact in owner.Contacts)
                {
                    if (contact != null && !items.Any(x => x.Id == contact.Id))
                    {
                        if (contact.Phones != null)
                        {
                            foreach (ContactPhone phone in contact.Phones)
                            {
                                if (phone != null)
                                {
                                    _context.Remove(phone);
                                }
                            }
                        }
                        if (contact.Addresses != null)
                        {
                            foreach (ContactAddress address in contact.Addresses)
                            {
                                if (address != null)
                                {
                                    _context.Remove(address);
                                }
                            }
                        }

                        _context.Remove(contact);
                    }
                }

                // replace Contacts.
                owner.Contacts = items.ToList();
                _context.Update(owner);
                _context.SaveChanges();

                return new ObjectResult(items);
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
        /// <param name="id">id of Owner to delete</param>
        /// <response code="200">OK</response>
        /// <response code="404">Owner not found</response>
        public virtual IActionResult OwnersIdDeletePostAsync(int id)
        {
            var exists = _context.Owners.Any(a => a.Id == id);
            if (exists)
            {
                var item = _context.Owners.First(a => a.Id == id);
                if (item != null)
                {
                    _context.Owners.Remove(item);
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
        /// <remarks>Gets an Owner&#39;s Equipment</remarks>
        /// <param name="id">id of Owner to fetch Equipment for</param>
        /// <response code="200">OK</response>
        public virtual IActionResult OwnersIdEquipmentGetAsync(int id)
        {
            var exists = _context.Owners.Any(a => a.Id == id);
            if (exists)
            {
                Owner owner = _context.Owners
                    .Include(x => x.EquipmentList)
                        .ThenInclude(y => y.EquipmentType)
                    .Include(x => x.EquipmentList)
                        .ThenInclude(x => x.LocalArea.ServiceArea.District.Region)
                    .Include(x => x.EquipmentList)
                        .ThenInclude(x => x.EquipmentType)
                    .Include(x => x.EquipmentList)
                        .ThenInclude(x => x.DumpTruck)
                    .Include(x => x.EquipmentList)
                        .ThenInclude(x => x.Owner)
                    .Include(x => x.EquipmentList)
                        .ThenInclude(x => x.EquipmentAttachments)
                    .Include(x => x.EquipmentList)
                        .ThenInclude(x => x.Notes)
                    .Include(x => x.EquipmentList)
                        .ThenInclude(x => x.Attachments)
                    .Include(x => x.EquipmentList)
                        .ThenInclude(x => x.History)
                    .First(a => a.Id == id);
                return new ObjectResult(owner.EquipmentList);
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
        /// <remarks>Replaces an Owner&#39;s Equipment</remarks>
        /// <param name="id">id of Owner to replace Equipment for</param>
        /// <param name="items">Replacement Owner Equipment.</param>
        /// <response code="200">OK</response>
        public virtual IActionResult OwnersIdEquipmentPutAsync(int id, Equipment[] items)
        {
            var exists = _context.Owners.Any(a => a.Id == id);
            if (exists && items != null)
            {
                Owner owner = _context.Owners
                    .Include(x => x.LocalArea.ServiceArea.District.Region)
                    .Include(x => x.EquipmentList)
                    .ThenInclude(y => y.EquipmentType)
                    .Include(x => x.Notes)
                    .Include(x => x.Attachments)
                    .Include(x => x.History)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Addresses)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Phones)
                    .First(x => x.Id == id);

                // adjust the incoming list.

                for (int i = 0; i < items.Count(); i++)
                {
                    Equipment item = items[i];
                    if (item != null)
                    {
                        bool equipment_exists = _context.Equipments.Any(x => x.Id == item.Id);
                        if (equipment_exists)
                        {
                            items[i] = _context.Equipments
                                .Include(x => x.LocalArea.ServiceArea.District.Region)
                                .Include(x => x.EquipmentType)
                                .Include(x => x.DumpTruck)
                                .Include(x => x.Owner)
                                .Include(x => x.EquipmentAttachments)
                                .Include(x => x.Notes)
                                .Include(x => x.Attachments)
                                .Include(x => x.History)
                                .First(x => x.Id == item.Id);
                        }
                        else
                        {
                            _context.Add(item);
                            items[i] = item;
                        }
                    }
                }

                // remove contacts that are no longer attached.
                List<Equipment> equipmentToRemove = new List<Equipment>();
                foreach (Equipment equipment in owner.EquipmentList)
                {
                    if (equipment != null && !items.Any(x => x.Id == equipment.Id))
                    {
                        equipmentToRemove.Add(equipment);                                             
                    }
                }

                if (equipmentToRemove.Count > 0)
                {
                    foreach (Equipment equipment in equipmentToRemove)
                    {
                        owner.EquipmentList.Remove(equipment);
                    }
                }                

                // replace Equipment List.
                owner.EquipmentList = items.ToList();
                _context.Update(owner);
                _context.SaveChanges();

                return new ObjectResult(items);
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
        /// <param name="id">id of Owner to fetch</param>
        /// <response code="200">OK</response>
        /// <response code="404">Owner not found</response>
        public virtual IActionResult OwnersIdGetAsync(int id)
        {
            var exists = _context.Owners.Any(a => a.Id == id);
            if (exists)
            {
                var result = _context.Owners
                    .Include(x => x.LocalArea.ServiceArea.District.Region)
                    .Include(x => x.EquipmentList)
                    .ThenInclude(y => y.EquipmentType)
                    .Include(x => x.Notes)
                    .Include(x => x.Attachments)
                    .Include(x => x.History)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Addresses)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Phones)
                    .First(a => a.Id == id);
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
        /// <param name="id">id of Owner to fetch</param>
        /// <param name="item"></param>
        /// <response code="200">OK</response>
        /// <response code="404">Owner not found</response>
        public virtual IActionResult OwnersIdPutAsync(int id, Owner item)
        {
            var exists = _context.Owners.Any(a => a.Id == id);
            if (exists && id == item.Id)
            {
                _context.Owners.Update(item);
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
        /// <response code="201">Owner created</response>
        public virtual IActionResult OwnersPostAsync(Owner item)
        {
            var exists = _context.Owners.Any(a => a.Id == item.Id);
            if (exists)
            {
                _context.Owners.Update(item);
            }
            else
            {
                // record not found
                _context.Owners.Add(item);
            }
            // Save the changes
            _context.SaveChanges();
            return new ObjectResult(item);
        }

        /// <summary>
        /// Searches Owners
        /// </summary>
        /// <remarks>Used for the owner search page.</remarks>
        /// <param name="localareas">Local Areas (array of id numbers)</param>
        /// <param name="equipmenttypes">Equipment Types (array of id numbers)</param>
        /// <param name="owner"></param>
        /// <param name="status">Status</param>
        /// <param name="hired">Hired</param>
        /// <response code="200">OK</response>
        public virtual IActionResult OwnersSearchGetAsync(int?[] localareas, int?[] equipmenttypes, int? owner, string status, bool? hired)
        {
            var data = _context.Owners
                    .Include(x => x.LocalArea.ServiceArea.District.Region)
                    .Include(x => x.EquipmentList)
                    .ThenInclude(y => y.EquipmentType)
                    .Include(x => x.Notes)
                    .Include(x => x.Attachments)
                    .Include(x => x.History)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Addresses)
                    .Include(x => x.Contacts)
                    .ThenInclude(y => y.Phones)
                    .Select(x => x);

            if (localareas != null)
            {
                foreach (int? localarea in localareas)
                {
                    if (localarea != null)
                    {
                        data = data.Where(x => x.LocalArea.Id == localarea);
                    }
                }
            }

            if (equipmenttypes != null)
            {
                foreach (int? item in equipmenttypes)
                {
                    if (item != null)
                    {
                        int equipmentType = (int) item;
                        data = data.Where(x => x.EquipmentList.Select (y => y.Id).ToList().Contains (equipmentType));
                    }
                }
            }
                        
            if (status != null)
            {
                data = data.Where(x => x.Status == status);
            }

            if (hired != null)
            {
                // hired is not currently implemented.                 
            }

            if (owner != null)
            {
                data = data.Where(x => x.Id == owner);
            }
            
            var result = data.ToList();
            return new ObjectResult(result);
        }
    }
}