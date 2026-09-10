using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<InventoryItem> InventoryItems {get;set;}
    public DbSet<Category> Categories { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
}
