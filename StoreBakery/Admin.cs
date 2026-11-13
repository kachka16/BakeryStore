using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreBakery
{
    [Table("admin")]
    public class Admin
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string login { get; set; }

        public string pass { get; set; }

        public Admin() { }

        public Admin(string login, string pass)
        {
            this.login = login;
            this.pass = pass;
        }
    }
}