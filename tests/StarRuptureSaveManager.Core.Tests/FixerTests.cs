using StarRuptureSaveFixer.Fixers;
using StarRuptureSaveFixer.Models;
using Newtonsoft.Json.Linq;

namespace StarRuptureSaveManager.Core.Tests;

public sealed class FixerTests
{
    [Fact]
    public void DroneFixer_RemovesDroneWithInvalidTarget()
    {
        var save = new SaveFile { JsonContent = BuildSaveJson(includeInvalidDrone: true, includeValidTarget: false) };
        var fixer = new DroneFixer();

        var changed = fixer.ApplyFix(save);

        Assert.True(changed);
        var root = JObject.Parse(save.JsonContent);
        var entities = (JObject)root["itemData"]!["Mass"]!["entities"]!;
        Assert.Null(entities["(ID=100)"]);
    }

    [Fact]
    public void DroneRemover_RemovesAllDroneEntities()
    {
        var save = new SaveFile { JsonContent = BuildSaveJson(includeInvalidDrone: true, includeValidTarget: true) };
        var fixer = new DroneRemover();

        var changed = fixer.ApplyFix(save);

        Assert.True(changed);
        var root = JObject.Parse(save.JsonContent);
        var entities = (JObject)root["itemData"]!["Mass"]!["entities"]!;
        Assert.Null(entities["(ID=100)"]);
        Assert.NotNull(entities["(ID=200)"]);
    }

    private static string BuildSaveJson(bool includeInvalidDrone, bool includeValidTarget)
    {
        var entities = new JObject
        {
            ["(ID=200)"] = new JObject
            {
                ["spawnData"] = new JObject
                {
                    ["entityConfigDataPath"] = "/Game/Chimera/Other/Entity.Entity"
                }
            }
        };

        if (includeValidTarget)
        {
            entities["(ID=300)"] = new JObject
            {
                ["spawnData"] = new JObject
                {
                    ["entityConfigDataPath"] = "/Game/Chimera/Other/Entity.Entity"
                }
            };
        }

        if (includeInvalidDrone)
        {
            entities["(ID=100)"] = new JObject
            {
                ["spawnData"] = new JObject
                {
                    ["entityConfigDataPath"] = "/Game/Chimera/Drones/DA_RailDroneConfig.DA_RailDroneConfig"
                },
                ["fragmentValues"] = new JArray
                {
                    "/Script/Chimera.CrLogisticsAgentFragment CurrentMovementStart=(ID=1) CurrentMovementTarget=(ID=999999)"
                }
            };
        }

        var root = new JObject
        {
            ["itemData"] = new JObject
            {
                ["Mass"] = new JObject
                {
                    ["entities"] = entities
                }
            }
        };

        return root.ToString(Newtonsoft.Json.Formatting.None);
    }
}
