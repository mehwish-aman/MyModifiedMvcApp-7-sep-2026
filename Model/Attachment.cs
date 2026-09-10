public class Attachment
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string FileName { get; set; }
    public string FileType { get; set; }
    public byte[] FileData { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public string ModifiedBy { get; set; }
}