using System;
using System.ComponentModel.DataAnnotations;

namespace Xavissa.Frontend.Models
{
    public class LocalSchemaInfo
    {
        [Key]
        public int Id { get; set; } = 1;
        public int Version { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
