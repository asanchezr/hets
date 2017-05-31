﻿using Hangfire.Console;
using Hangfire.Server;
using HETSAPI.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace HETSAPI.Import

{
    public class ImportLocalArea
    {
        const string oldTable = "Area";
        const string newTable = "LocalArea";
        const string xmlFileName = "Area.xml";

        /// <summary>
        /// Import local areas
        /// </summary>
        /// <param name="performContext"></param>
        /// <param name="dbContext"></param>
        /// <param name="fileLocation"></param>
        static public void Import(PerformContext performContext, DbAppContext dbContext, string fileLocation, string systemId)
        {
            try
            {
                string rootAttr = "ArrayOf" + oldTable;

                performContext.WriteLine("Processing Local Areas");

                var progress = performContext.WriteProgressBar();
                progress.SetValue(0);

                // create serializer and serialize xml file
                XmlSerializer ser = new XmlSerializer(typeof(HETSAPI.Import.Area[]), new XmlRootAttribute(rootAttr));
                MemoryStream memoryStream = ImportUtility.memoryStreamGenerator(xmlFileName, oldTable, fileLocation, rootAttr);
                HETSAPI.Import.Area[] legacyItems = (HETSAPI.Import.Area[])ser.Deserialize(memoryStream);
                foreach (var item in legacyItems.WithProgress(progress))
                {
                    LocalArea localArea = null;
                    // see if we have this one already.
                    ImportMap importMap = dbContext.ImportMaps.FirstOrDefault(x => x.OldTable == oldTable && x.OldKey == item.Area_Id.ToString());
                    if (dbContext.LocalAreas.Where(x => x.Name.ToUpper() == item.Area_Desc.Trim().ToUpper()).Count() > 0)
                    {
                        localArea = dbContext.LocalAreas.FirstOrDefault(x => x.Name.ToUpper() == item.Area_Desc.Trim().ToUpper());
                    }

                    if (importMap == null || dbContext.LocalAreas.Where(x => x.Name.ToUpper() == item.Area_Desc.Trim().ToUpper()).Count() == 0) // new entry
                    {
                        if (item.Area_Id > 0)
                        {
                            CopyToInstance(performContext, dbContext, item, ref localArea, systemId);
                            ImportUtility.AddImportMap(dbContext, oldTable, item.Area_Id.ToString(), newTable, localArea.Id);
                        }
                    }
                    else // update
                    {
                        localArea = dbContext.LocalAreas.FirstOrDefault(x => x.Id == importMap.NewKey);
                        if (localArea == null) // record was deleted
                        {
                            CopyToInstance(performContext, dbContext, item, ref localArea, systemId);
                            // update the import map.
                            importMap.NewKey = localArea.Id;
                            dbContext.ImportMaps.Update(importMap);
                        }
                        else // ordinary update.
                        {
                            CopyToInstance(performContext, dbContext, item, ref localArea, systemId);
                            // touch the import map.
                            importMap.LastUpdateTimestamp = DateTime.UtcNow;
                            dbContext.ImportMaps.Update(importMap);
                        }
                    }
                }
                performContext.WriteLine("*** Done ***");
            }

            catch (Exception e)
            {
                performContext.WriteLine("*** ERROR ***");
                performContext.WriteLine(e.ToString());
            }

            try
            {
                int iResult = dbContext.SaveChangesForImport();
            }
            catch (Exception e)
            {
                performContext.WriteLine("*** ERROR With add or update Local Area ***");
                performContext.WriteLine(e.ToString());
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="oldObject"></param>
        /// <param name="localArea"></param>
        static private void CopyToInstance(PerformContext performContext, DbAppContext dbContext, HETSAPI.Import.Area oldObject, ref LocalArea localArea, string systemId)
        {
            bool isNew = false;

            if (oldObject.Area_Id <= 0)
                return;
            if (localArea == null )
            {
                isNew = true;
                localArea = new LocalArea();
                localArea.Id = oldObject.Area_Id;
            }
            try
            {
                localArea.Name = oldObject.Area_Desc.Trim();
            }
            catch (Exception e)
            {
                string istr = e.ToString();
            }

            try
            {
                ServiceArea serviceArea = dbContext.ServiceAreas.FirstOrDefault(x => x.MinistryServiceAreaID == oldObject.Service_Area_Id);
                localArea.ServiceArea = serviceArea;
            }
            catch (Exception e)
            {
                string iStr = e.ToString();
            }


            if (isNew)
            {
                localArea.CreateUserid = systemId;
                localArea.CreateTimestamp = DateTime.UtcNow;
                dbContext.LocalAreas.Add(localArea);
            }
            else
            {
                localArea.LastUpdateUserid = systemId;
                localArea.LastUpdateTimestamp = DateTime.UtcNow;
                dbContext.LocalAreas.Update(localArea);
            }
        }
    }
}

