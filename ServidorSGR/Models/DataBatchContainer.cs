
using MessagePack;
using System.Collections.Generic;

namespace ServidorSGR.Models
{
    [MessagePackObject]
    public class DataBatchContainer
    {
        [Key(0)]
        public List<DataBatch> Batches { get; set; } = new List<DataBatch>();
    }
}