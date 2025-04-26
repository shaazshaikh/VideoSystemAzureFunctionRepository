using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoSystemAzureFunction.Models
{
    public class VideoModel
    {
        public string UserId { get; set; }
        public string FileId { get; set; }
        public string BlobUri { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string BlobName { get; set; }
    }
}
