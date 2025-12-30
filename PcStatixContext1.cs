using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardwareMonitor.Models
{
    [Table("ekrankarti")]
    public class EkranKarti
    {

        public int id { get; set; }
        public required string ekrank_isim { get; set; }
        public int ekrank_sicaklik { get; set; }
    }
    [Table("islemci")]
    public class Islemci
    {
        public int id { get; set; }
        public required string islemci_isim { get; set; }
        public int islemci_sicaklik { get; set; }
    }






}
