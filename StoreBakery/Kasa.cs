using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SQLite;

namespace StoreBakery
{
    [Table("kasa")]
    public class Kasa
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string login { get; set; }
        public string pass { get; set; }

        public Kasa() { }

        public Kasa(string login, string pass)
        {
            this.login = login;
            this.pass = pass;
        }
    }

    public static class AppDb
    {
        public const string ConnStr = "Data Source=bakery_db.db;Version=3;";

        public static SQLiteConnection Open()
        {
            var connection = new SQLiteConnection(ConnStr);
            connection.Open();
            return connection;
        }
    }
}