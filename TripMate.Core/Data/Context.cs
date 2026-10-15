using Microsoft.EntityFrameworkCore;
using TripMate.Core.Models;

namespace TripMate.Core.Data
{
    public class Context : DbContext
    {
        // Location of the SQLite database file
        private readonly string databasePath;

        // Default database: TripMate.db next to the application
        public Context() : this(Path.Combine(AppContext.BaseDirectory, "TripMate.db"))
        {
        }

        // Constructor used by the unit tests to point at a temporary database
        public Context(string databasePath)
        {
            this.databasePath = databasePath;
        }

        // To perform CRUD = Create / Read / Update / Delete
        // DbSet<TEntity> to query and save instances
        public DbSet<Trip> Trips { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseShare> ExpenseShares { get; set; }

        // Configure database access
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // extension method
            // Connection String = "Data Source=TripMate.db"
            optionsBuilder.UseSqlite("Data Source=" + databasePath);
        }

        // Configure the relationships that EF cannot work out by convention
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // An Expense has one PaidBy member; a Member can pay for many expenses
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.PaidBy)
                .WithMany()
                .HasForeignKey(e => e.PaidById);

            // Removing an expense removes its shares
            modelBuilder.Entity<ExpenseShare>()
                .HasOne(s => s.Expense)
                .WithMany(e => e.Shares)
                .HasForeignKey(s => s.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}