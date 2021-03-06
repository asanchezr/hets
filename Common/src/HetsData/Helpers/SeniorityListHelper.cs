﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using HetsData.Model;

namespace HetsData.Helpers
{
    #region Seniority List Models

    public class RuleType
    {
        public int Default { get; set; }
        public int DumpTruck { get; set; }
    }

    public class ScoringRules
    {
        public RuleType EquipmentScore { get; set; }
        public RuleType BlockSize { get; set; }
        public RuleType TotalBlocks { get; set; }
    }

    public class SeniorityViewModel
    {
        public int Id { get; set; }
        public string EquipmentType { get; set; }
        public string OwnerName { get; set; }
        public int? OwnerId { get; set; }
        public string SeniorityString { get; set; }
        public string Seniority { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string Size { get; set; }
        public string EquipmentCode { get; set; }
        public string LastCalled { get; set; }
        public string YearsRegistered { get; set; }
        public string YtdHours { get; set; }
        public string HoursYearMinus1 { get; set; }
        public string HoursYearMinus2 { get; set; }
        public string HoursYearMinus3 { get; set; }
        public int SenioritySortOrder { get; set; }
    }

    public class SeniorityListRecord
    {
        public string DistrictName { get; set; }
        public string LocalAreaName { get; set; }
        public string DistrictEquipmentTypeName { get; set; }
        public string YearMinus1 { get; set; }
        public string YearMinus2 { get; set; }
        public string YearMinus3 { get; set; }
        public List<SeniorityViewModel> SeniorityList { get; set; }
    }

    public class SeniorityListPdfViewModel
    {        
        public List<SeniorityListRecord> SeniorityListRecords { get; set; }
                
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    #endregion

    /// <summary>
    /// Seniority List Helper
    /// </summary>
    public static class SeniorityListHelper
    {       
        #region Manage the Seniority List for a Specific Location

        /// <summary>
        /// Calculate the Seniority List
        /// </summary>        
        /// <param name="localAreaId"></param>
        /// <param name="districtEquipmentTypeId"></param>
        /// <param name="equipmentTypeId"></param>
        /// <param name="context"></param>
        /// <param name="configuration"></param>
        public static void CalculateSeniorityList(int localAreaId, int districtEquipmentTypeId, 
            int equipmentTypeId, DbAppContext context, IConfiguration configuration)
        {
            try
            {
                // validate data
                if (context != null &&
                    context.HetLocalArea.Any(x => x.LocalAreaId == localAreaId) &&
                    context.HetDistrictEquipmentType.Any(x => x.DistrictEquipmentTypeId == districtEquipmentTypeId))
                {
                    // get processing rules
                    SeniorityScoringRules scoringRules = new SeniorityScoringRules(configuration);

                    // get the associated equipment type
                    HetEquipmentType equipmentTypeRecord = context.HetEquipmentType
                        .FirstOrDefault(x => x.EquipmentTypeId == equipmentTypeId);

                    if (equipmentTypeRecord != null)
                    {
                        // get rules                  
                        int seniorityScoring = equipmentTypeRecord.IsDumpTruck ? scoringRules.GetEquipmentScore("DumpTruck") : scoringRules.GetEquipmentScore();
                        int blockSize = equipmentTypeRecord.IsDumpTruck ? scoringRules.GetBlockSize("DumpTruck") : scoringRules.GetBlockSize();
                        int totalBlocks = equipmentTypeRecord.IsDumpTruck ? scoringRules.GetTotalBlocks("DumpTruck") : scoringRules.GetTotalBlocks();

                        // get all equipment records
                        IQueryable<HetEquipment> data = context.HetEquipment
                             .Where(x => x.LocalAreaId == localAreaId &&
                                         x.DistrictEquipmentTypeId == districtEquipmentTypeId)
                             .Select(x => x);

                        // update the seniority score
                        foreach (HetEquipment equipment in data)
                        {
                            if (equipment.EquipmentStatusType.EquipmentStatusTypeCode != HetEquipment.StatusApproved)
                            {
                                equipment.SeniorityEffectiveDate = DateTime.UtcNow;
                                equipment.BlockNumber = null;
                                equipment.Seniority = null;
                                equipment.NumberInBlock = null;
                            }
                            else
                            {
                                equipment.CalculateSeniority(seniorityScoring);
                                equipment.SeniorityEffectiveDate = DateTime.UtcNow;
                            }

                            context.HetEquipment.Update(equipment);
                        }

                        context.SaveChanges();

                        // put equipment into the correct blocks
                        AssignBlocks(localAreaId, districtEquipmentTypeId, blockSize, totalBlocks, context);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: CalculateSeniorityList");
                Console.WriteLine(e);
                throw;
            }
        }

        /// <summary>
        /// Assign blocks for the given local area and equipment type
        /// </summary>        
        /// <param name="localAreaId"></param>
        /// <param name="districtEquipmentTypeId"></param>
        /// <param name="blockSize"></param>
        /// <param name="totalBlocks"></param>
        /// <param name="context"></param>
        /// <param name="saveChanges"></param>
        public static void AssignBlocks(int localAreaId, int districtEquipmentTypeId, 
            int blockSize, int totalBlocks, DbAppContext context, bool saveChanges = true)
        {
            try
            {
                // get all equipment records
                List<HetEquipment> data = context.HetEquipment
                    .Include(x => x.Owner)
                    .Where(x => x.EquipmentStatusType.EquipmentStatusTypeCode == HetEquipment.StatusApproved &&
                                x.LocalArea.LocalAreaId == localAreaId &&
                                x.DistrictEquipmentTypeId == districtEquipmentTypeId)
                    .OrderByDescending(x => x.Seniority).ThenBy(x => x.ReceivedDate)
                    .Select(x => x)
                    .ToList();

                // total blocks only counts the "main" blocks - we need to add 1 more for the remaining records
                totalBlocks = totalBlocks + 1;

                // instantiate lists to hold equipment by block
                List<int>[] blocks = new List<int>[totalBlocks];

                foreach (HetEquipment equipment in data)
                {
                    // iterate the blocks and add the record
                    for (int i = 0; i < totalBlocks; i++)
                    {
                        if (AddedToBlock(i, totalBlocks, blockSize, blocks, equipment, context, saveChanges))
                        {
                            break; // move to next record
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: AssignBlocks");
                Console.WriteLine(e);
                throw;
            }
        }

        private static bool AddedToBlock(int currentBlock, int totalBlocks, int blockSize, 
            List<int>[] blocks, HetEquipment equipment, DbAppContext context, bool saveChanges = true)
        {
            try
            {
                // check if this record's Owner is null
                if (equipment.Owner == null)
                {
                    return false; // not adding this record to the block
                }

                if (blocks[currentBlock] == null)
                {
                    blocks[currentBlock] = new List<int>();
                }

                // check if the current block is full
                if (currentBlock < (totalBlocks - 1) && blocks[currentBlock].Count >= blockSize)
                {
                    return false; // not adding this record to the block
                }

                // check if this record's Owner already exists in the block   
                if (currentBlock < (totalBlocks - 1) && blocks[currentBlock].Contains(equipment.Owner.OwnerId))
                {
                    return false; // not adding this record to the block
                }

                // add record to the block                        
                blocks[currentBlock].Add(equipment.Owner.OwnerId);

                // update the equipment record
                equipment.BlockNumber = currentBlock + 1;
                equipment.NumberInBlock = blocks[currentBlock].Count;

                context.HetEquipment.Update(equipment);

                if (saveChanges)
                {
                    context.SaveChanges();
                }

                // record added to the block
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: AddedToBlock");
                Console.WriteLine(e);
                throw;
            }
        }

        #endregion

        #region Convert full equipment model record to a "Lite" seniority version

        /// <summary>
        /// Seniority List view model
        /// </summary>
        /// <param name="model"></param>
        /// <param name="scoringRules"></param>
        /// <param name="rotationList"></param>
        /// <param name="context"></param>        
        /// <returns></returns>
        public static SeniorityViewModel ToSeniorityViewModel(HetEquipment model, SeniorityScoringRules scoringRules,
            HetLocalAreaRotationList rotationList, DbAppContext context)
        {
            SeniorityViewModel seniorityViewModel = new SeniorityViewModel();

            if (model == null) return seniorityViewModel;

            int numberOfBlocks = 0;

            // get number of blocks for this equipment type
            if (model.DistrictEquipmentType != null)
            {
                numberOfBlocks = model.DistrictEquipmentType.EquipmentType.IsDumpTruck
                    ? scoringRules.GetTotalBlocks("DumpTruck") + 1
                    : scoringRules.GetTotalBlocks() + 1;
            }

            // get equipment seniority
            float seniority = 0F;
            if (model.Seniority != null)
            {
                seniority = (float)model.Seniority;
            }

            // get equipment block number
            int blockNumber = 0;
            if (model.BlockNumber != null)
            {
                blockNumber = (int)model.BlockNumber;
            }

            // get equipment block number
            int numberInBlock = 0;
            if (model.NumberInBlock != null)
            {
                numberInBlock = (int)model.NumberInBlock;
            }

            // *************************************************************
            // check if this record/owner was call last
            // *************************************************************
            bool callNext = false;

            if (rotationList != null &&
                blockNumber == 1 &&
                rotationList.AskNextBlock1Id == model.EquipmentId)
            {
                callNext = true;
            }
            else if (rotationList != null &&
                     numberOfBlocks > 1 &&
                     blockNumber == 2 &&
                     rotationList.AskNextBlock2Id == model.EquipmentId)
            {
                callNext = true;
            }
            else if (rotationList != null &&
                     rotationList.AskNextBlockOpenId == model.EquipmentId)
            {
                callNext = true;
            }

            seniorityViewModel.LastCalled = callNext ? "Y" : " ";

            // *************************************************************
            // Map data to view model
            // *************************************************************
            seniorityViewModel.Id = model.EquipmentId;

            if (model.DistrictEquipmentType != null)
            {
                seniorityViewModel.EquipmentType = model.DistrictEquipmentType.DistrictEquipmentName;
            }

            if (model.Owner != null)
            {
                seniorityViewModel.OwnerName = model.Owner.OrganizationName;
                seniorityViewModel.OwnerId = model.OwnerId;
            }

            seniorityViewModel.SeniorityString = EquipmentHelper.FormatSeniorityString(seniority, blockNumber, numberOfBlocks);

            // format the seniority value
            seniorityViewModel.Seniority = string.Format("{0:0.###}", model.Seniority);

            seniorityViewModel.Make = model.Make;
            seniorityViewModel.Model = model.Model;
            seniorityViewModel.Size = model.Size;
            seniorityViewModel.EquipmentCode = model.EquipmentCode;

            seniorityViewModel.YearsRegistered = model.YearsOfService.ToString();

            // calculate and format the ytd hours
            float tempHours = EquipmentHelper.GetYtdServiceHours(model.EquipmentId, context);
            seniorityViewModel.YtdHours = string.Format("{0:0.###}", tempHours);

            // format the hours
            seniorityViewModel.HoursYearMinus1 = string.Format("{0:0.###}", model.ServiceHoursLastYear);
            seniorityViewModel.HoursYearMinus2 = string.Format("{0:0.###}", model.ServiceHoursTwoYearsAgo);
            seniorityViewModel.HoursYearMinus3 = string.Format("{0:0.###}", model.ServiceHoursThreeYearsAgo);

            // get last called value


            seniorityViewModel.SenioritySortOrder = EquipmentHelper.CalculateSenioritySortOrder(blockNumber, numberInBlock);

            return seniorityViewModel;
        }

        #endregion

        /*
        /// <summary>
        /// Hangfire job to do the Annual Rollover tasks
        /// </summary>
        /// <param name="context"></param>
        /// <param name="connectionString"></param>
        /// <param name="configuration"></param>
        public static void AnnualRolloverJob(PerformContext context, string connectionString, IConfiguration configuration)
        {
            try
            {
                // open a connection to the database
                DbContextOptionsBuilder<DbAppContext> options = new DbContextOptionsBuilder<DbAppContext>();
                options.UseNpgsql(connectionString);
                DbAppContext dbContext = new DbAppContext(null, options.Options);

                // get processing rules
                SeniorityScoringRules scoringRules = new SeniorityScoringRules(configuration);

                // update progress bar
                IProgressBar progress = context.WriteProgressBar();
                context.WriteLine("Starting Annual Rollover Job");

                progress.SetValue(0);

                // get all equipment types
                List<EquipmentType> equipmentTypes = dbContext.EquipmentTypes.ToList();

                // The annual rollover will process all local areas in turn
                List<LocalArea> localAreas = dbContext.LocalAreas.ToList();

                foreach (LocalArea localArea in localAreas.WithProgress(progress))
                {
                    if (localArea.Name != null)
                    {
                        context.WriteLine("Local Area: " + localArea.Name);
                    }
                    else
                    {
                        context.WriteLine("Local Area ID: " + localArea.Id);
                    }

                    foreach (EquipmentType equipmentType in equipmentTypes)
                    {
                        // it this a dump truck? 
                        bool isDumpTruck = equipmentType.IsDumpTruck;

                        // get rules for scoring and seniority block
                        int seniorityScoring = isDumpTruck ? scoringRules.GetEquipmentScore("DumpTruck") : scoringRules.GetEquipmentScore();
                        int blockSize = isDumpTruck ? scoringRules.GetBlockSize("DumpTruck") : scoringRules.GetBlockSize();
                        int totalBlocks = isDumpTruck ? scoringRules.GetTotalBlocks("DumpTruck") : scoringRules.GetTotalBlocks();

                        using (DbAppContext etContext = new DbAppContext(null, options.Options))
                        {
                            List<Equipment> data = etContext.Equipments
                                .Include(x => x.LocalArea)
                                .Include(x => x.DistrictEquipmentType.EquipmentType)
                                .Where(x => x.Status == Equipment.StatusApproved &&
                                            x.LocalArea.Id == localArea.Id &&
                                            x.DistrictEquipmentType.EquipmentType.Id == equipmentType.Id)
                                .Select(x => x)
                                .ToList();

                            foreach (Equipment equipment in data)
                            {
                                // rollover the year
                                equipment.ServiceHoursThreeYearsAgo = equipment.ServiceHoursTwoYearsAgo;
                                equipment.ServiceHoursTwoYearsAgo = equipment.ServiceHoursLastYear;
                                equipment.ServiceHoursLastYear = equipment.GetYtdServiceHours(dbContext);
                                equipment.CalculateYearsOfService(DateTime.UtcNow);

                                // blank out the override reason
                                equipment.SeniorityOverrideReason = "";

                                // update the seniority score
                                equipment.CalculateSeniority(seniorityScoring);

                                etContext.Equipments.Update(equipment);
                                etContext.SaveChanges();
                                etContext.Entry(equipment).State = EntityState.Detached;
                            }
                        }

                        // now update the rotation list
                        using (DbAppContext abContext = new DbAppContext(null, options.Options))
                        {
                            int localAreaId = localArea.Id;
                            int equipmentTypeId = equipmentType.Id;

                            AssignBlocks(abContext, localAreaId, equipmentTypeId, blockSize, totalBlocks);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        */
    }

    #region Seniority Scoring Rules

    /// <summary>
    /// Object to Manage Scoring Rules
    /// </summary>
    public class SeniorityScoringRules
    {
        private readonly string DefaultConstant = "Default";

        private readonly Dictionary<string, int> _equipmentScore = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _blockSize = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _totalBlocks = new Dictionary<string, int>();

        /// <summary>
        /// Scoring Rules Constructor
        /// </summary>
        /// <param name="configuration"></param>
        public SeniorityScoringRules(IConfiguration configuration)
        {
            try
            {
                IEnumerable<IConfigurationSection> root = configuration.GetChildren();

                foreach (IConfigurationSection section in root)
                {
                    if (string.Equals(section.Key.ToLower(), "SeniorityScoringRules", StringComparison.OrdinalIgnoreCase))
                    {
                        // get children
                        IEnumerable<IConfigurationSection> ruleSections = section.GetChildren();

                        foreach (IConfigurationSection ruleSection in ruleSections)
                        {
                            string ruleSectionName = ruleSection.Key;

                            IEnumerable<IConfigurationSection> rules = ruleSection.GetChildren();

                            foreach (IConfigurationSection rule in rules)
                            {
                                string name = rule.Key;
                                int value = Convert.ToInt32(rule.Value);

                                switch (ruleSectionName)
                                {
                                    case "EquipmentScore":
                                        _equipmentScore.Add(name, value);
                                        break;

                                    case "BlockSize":
                                        _blockSize.Add(name, value);
                                        break;

                                    case "TotalBlocks":
                                        _totalBlocks.Add(name, value);
                                        break;

                                    default:
                                        throw new ArgumentException("Invalid seniority scoring rules");
                                }
                            }
                        }

                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public SeniorityScoringRules(string seniorityScoringRules)
        {
            try
            {
                // convert string to json object
                ScoringRules rules = JsonConvert.DeserializeObject<ScoringRules>(seniorityScoringRules);

                // Equipment Score
                _equipmentScore.Add("Default", rules.EquipmentScore.Default);
                _equipmentScore.Add("DumpTruck", rules.EquipmentScore.DumpTruck);

                // Block Size
                _blockSize.Add("Default", rules.BlockSize.Default);
                _blockSize.Add("DumpTruck", rules.BlockSize.DumpTruck);

                // Total Blocks"
                _totalBlocks.Add("Default", rules.TotalBlocks.Default);
                _totalBlocks.Add("DumpTruck", rules.TotalBlocks.DumpTruck);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public int GetEquipmentScore(string type = null)
        {
            if (string.IsNullOrEmpty(type))
            {
                type = DefaultConstant;
            }

            return _equipmentScore[type];
        }

        public int GetBlockSize(string type = null)
        {
            if (string.IsNullOrEmpty(type))
            {
                type = DefaultConstant;
            }

            return _blockSize[type];
        }

        public int GetTotalBlocks(string type = null)
        {
            if (string.IsNullOrEmpty(type))
            {
                type = DefaultConstant;
            }

            return _totalBlocks[type];
        }
    }

    #endregion
}
