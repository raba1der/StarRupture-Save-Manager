using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Utils;

namespace StarRuptureSaveFixer.Fixers;

/// <summary>
/// Fixes drone-related issues in the save file
/// </summary>
public class DroneFixer : IFixer
{
    public string Name => "Drone Fixer";

    public bool ApplyFix(SaveFile saveFile)
    {
        ConsoleLogger.Info($"Applying fix: {Name}");

        try
        {
            // Parse the JSON using Newtonsoft.Json
            JObject root = JObject.Parse(saveFile.JsonContent);

            // Navigate to itemData > Mass > entities
            JToken? itemData = root["itemData"];
            if (itemData == null)
            {
                ConsoleLogger.Warning("'itemData' not found in JSON.");
                return false;
            }

            JToken? mass = itemData["Mass"];
            if (mass == null)
            {
                ConsoleLogger.Warning("'Mass' not found in itemData.");
                return false;
            }

            JObject? entities = mass["entities"] as JObject;
            if (entities == null)
            {
                ConsoleLogger.Warning("'entities' not found in Mass.");
                return false;
            }

            // Collect properties once
            var props = entities.Properties().ToList();
            int totalEntities = props.Count;
            ConsoleLogger.Progress($"Scanning {totalEntities} entities for drone candidates...");

            // First pass: collect drone candidates while reporting progress
            var droneCandidates = new List<JProperty>();
            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];

                if (IsDroneEntity(prop.Value, out _))
                {
                    droneCandidates.Add(prop);
                }

                // Periodically report scanning progress so UI doesn't look frozen
                if (i % 50 == 0 || i == props.Count - 1)
                {
                    ConsoleLogger.Progress($"Scanning entities: {i + 1}/{totalEntities}");
                }
            }

            int totalDrones = droneCandidates.Count;
            ConsoleLogger.Info($"Found {totalDrones} drone(s) to check.");
            ConsoleLogger.Plain("");

            // Find all drone entities and check their validity with detailed progress
            List<string> dronesToDelete = new();

            for (int idx = 0; idx < droneCandidates.Count; idx++)
            {
                var entity = droneCandidates[idx];
                string entityKey = entity.Name;
                JToken entityValue = entity.Value;

                // Report which drone we're checking
                ConsoleLogger.Progress($"Checking drone {idx + 1}/{totalDrones}: {entityKey}");

                // Extract movement IDs from the logistics fragment
                if (IsDroneEntity(entityValue, out string? logisticsFragment))
                {
                    if (ExtractMovementIds(logisticsFragment!, out int? currentMovementStart, out int? currentMovementTarget))
                    {
                        // Check if CurrentMovementTarget points to a valid entity
                        if (currentMovementTarget.HasValue)
                        {
                            string targetEntityKey = $"(ID={currentMovementTarget.Value})";
                            if (entities[targetEntityKey] == null)
                            {
                                dronesToDelete.Add(entityKey);
                            }
                        }
                    }
                }

                // Optional tiny pause to keep UI responsive if needed
                // System.Threading.Thread.Sleep(0);
            }

            ConsoleLogger.Info($"Checked {dronesToDelete.Count + (totalDrones - dronesToDelete.Count)}/{totalDrones} drone(s).");
            ConsoleLogger.Info($"Found {dronesToDelete.Count} invalid drone(s) to delete.");

            // Delete invalid drones
            if (dronesToDelete.Count > 0)
            {
                foreach (string droneKey in dronesToDelete)
                {
                    entities.Remove(droneKey);
                }

                // Serialize back to JSON without formatting
                saveFile.JsonContent = root.ToString(Newtonsoft.Json.Formatting.None);

                ConsoleLogger.Success($"Successfully removed {dronesToDelete.Count} invalid drone(s).\n");
                return true;
            }
            else
            {
                ConsoleLogger.Info("No invalid drones found. Save file is clean.");
                return false;
            }
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            ConsoleLogger.Error($"JSON parsing error: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Error during drone fix: {ex.Message}");
            ConsoleLogger.Error($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Checks if an entity is a drone based on the specified criteria
    /// </summary>
    private bool IsDroneEntity(JToken entity, out string? logisticsFragment)
    {
        logisticsFragment = null;

        try
        {
            // Check for spawnData.entityConfigDataPath
            JToken? spawnData = entity["spawnData"];
            if (spawnData == null)
                return false;

            JToken? configPath = spawnData["entityConfigDataPath"];
            if (configPath == null)
                return false;

            string configPathValue = configPath.ToString();
            if (configPathValue != "/Game/Chimera/Drones/DA_RailDroneConfig.DA_RailDroneConfig")
                return false;

            // Check for fragmentValues at the entity level (not under spawnData)
            JToken? fragmentValues = entity["fragmentValues"];
            if (fragmentValues == null)
                return false;

            if (fragmentValues is JArray fragmentArray)
            {
                foreach (JToken fragment in fragmentArray)
                {
                    string fragmentStr = fragment.ToString();
                    if (fragmentStr.StartsWith("/Script/Chimera.CrLogisticsAgentFragment"))
                    {
                        logisticsFragment = fragmentStr;
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts CurrentMovementStart and CurrentMovementTarget IDs from the fragment string
    /// </summary>
    private bool ExtractMovementIds(string fragment, out int? currentMovementStart, out int? currentMovementTarget)
    {
        currentMovementStart = null;
        currentMovementTarget = null;

        try
        {
            // Use regex to find CurrentMovementStart=(ID=xxx)
            var startMatch = Regex.Match(fragment, @"CurrentMovementStart=\(ID=(\d+)\)");
            if (startMatch.Success)
            {
                currentMovementStart = int.Parse(startMatch.Groups[1].Value);
            }

            // Use regex to find CurrentMovementTarget=(ID=xxx)
            var targetMatch = Regex.Match(fragment, @"CurrentMovementTarget=\(ID=(\d+)\)");
            if (targetMatch.Success)
            {
                currentMovementTarget = int.Parse(targetMatch.Groups[1].Value);
            }

            return currentMovementStart.HasValue || currentMovementTarget.HasValue;
        }
        catch
        {
            return false;
        }
    }
}
