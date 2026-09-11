using System.ComponentModel.DataAnnotations;    //data annotation ka use kr k hum model ki properties ko validate
// kr skty hain, jaise max length, range, required etc

public class InventoryItem
{
    public int Id { get; set; }   //EF core isy automatically primary key samjh lega

    [MaxLength(100)]
    public string ItemName { get; set; }

    [MaxLength(250)]
    public string Description { get; set; }

    [Range(0, 999999)]
    public decimal PurchaseValue { get; set; } =0;

    public DateTime PurchaseDate { get; set; }   

    [Range(0, 999999)]
    public decimal Tax { get; set; } = 0;

    [MaxLength(100)]
    public string Company { get; set; }

    [Range(0, 999999)]   //for limiting to 6 digits only
    public decimal Total { get; set; }=0;  //default value set kr di hai visual hint.
    [Range(0, 999999)]
    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }
    public int AttachmentCount { get; set; }
     public bool IsDeleted { get; set; } = false;
}