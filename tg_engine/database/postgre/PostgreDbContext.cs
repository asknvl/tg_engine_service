using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.models;

namespace tg_engine.database.postgre
{
    public class PostgreDbContext : DbContext
    {
        public DbSet<account> accounts { get; set; }
        public DbSet<channel> channels { get; set; }
        public DbSet<source> sources { get; set; }
        public DbSet<channel_account> channels_accounts { get; set; }   
        public DbSet<telegram_chat> telegram_chats { get; set; }
        public DbSet<telegram_user> telegram_users { get; set; }    

        public PostgreDbContext(DbContextOptions<PostgreDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Specify the schema name
            modelBuilder.Entity<account>().ToTable("accounts", schema: "app_data");
            modelBuilder.Entity<channel>().ToTable("channels", schema: "app_data");
            modelBuilder.Entity<source>().ToTable("sources", schema: "app_data");
            modelBuilder.Entity<channel_account>().ToTable("channel_account", schema: "app_data");
            modelBuilder.Entity<telegram_chat>().ToTable("telegram_chats", schema: "app_data");
            modelBuilder.Entity<telegram_user>().ToTable("telegram_users", schema: "app_data");
        }
    }
}
