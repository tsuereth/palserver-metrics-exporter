using System.Collections.Generic;
using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/settings
    // NOTE: Actual results may not match the documentation. :)
    //
    // MORE NOTE:
    // This is NOT a complete representation of the server-settings API response.
    // As the full schema is quite large - including old, deprecated settings -
    // this schema only represents a subset of values relevant to server monitoring.
    public class PalServerSettings
    {
        public double BaseCampMaxNum { get; set; }

        public double BaseCampMaxNumInGuild { get; set; }

        public double BaseCampWorkerMaxNum { get; set; }

        public double ItemContainerForceMarkDirtyInterval { get; set; }

        public double MaxBuildingLimitNum { get; set; }

        public double PhysicsActiveDropItemMaxNum { get; set; }

        public double ServerReplicatePawnCullDistance { get; set; }
    }
}
