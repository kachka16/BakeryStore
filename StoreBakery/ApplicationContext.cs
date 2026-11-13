using System.Data.Entity;

namespace StoreBakery
{
    public class ApplicationContext : DbContext
    {
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Kasa> Kasa { get; set; }

        public ApplicationContext() : base("DefaultConnection")
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<ApplicationContext>());
        }
    }
}