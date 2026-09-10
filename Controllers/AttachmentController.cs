using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc; //enabling mvc services
using Microsoft.Data.SqlClient;
using System.Data;   //for direction parameter for id which we used in insert func
using Microsoft.EntityFrameworkCore;
public class AttachmentsController : Controller
{
    private readonly AppDbContext _context;

    public AttachmentsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(int inventoryItemId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file selected." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { message = "File exceeds 5MB limit." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var fileData = ms.ToArray();

        var msgParam = new SqlParameter("@msg", SqlDbType.NVarChar, 250)
        {
            Direction = ParameterDirection.Output
        };

        _context.Database.ExecuteSqlRaw(
            "EXEC Attachments_Manage @InventoryItemId = {0}, @FileName = {1}, @FileType = {2}, @FileData = {3}, @msg = {4} OUTPUT",
            inventoryItemId, file.FileName, file.ContentType, fileData, msgParam
        );

        string msg = msgParam.Value?.ToString() ?? "";
        return Ok(new { message = msg, fileName = file.FileName });
    }

    [HttpGet]
    public IActionResult GetByItem(int inventoryItemId)
    {
        var results = new List<object>();

        using (var connection = new SqlConnection(_context.Database.GetConnectionString()))
        {
            using (var command = new SqlCommand("Attachments_Manage", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@InventoryItemId", inventoryItemId);
                command.Parameters.Add("@msg", SqlDbType.NVarChar, 250).Direction = ParameterDirection.Output;

                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new
                        {
                            id = reader.GetInt32(reader.GetOrdinal("Id")),
                            fileName = reader.GetString(reader.GetOrdinal("FileName")),
                            fileType = reader.GetString(reader.GetOrdinal("FileType"))
                        });
                    }
                }
            }
        }

        return Ok(results);
    }

    [HttpPost]
    [Route("Attachments/Delete/{id}")]
    public IActionResult Delete(int id)
    {
        try
        {
            var msgParam = new SqlParameter("@msg", SqlDbType.NVarChar, 250)
            {
                Direction = ParameterDirection.Output
            };

            var rowsAffected = _context.Database.ExecuteSqlRaw(
                "EXEC Attachments_Manage @Id = {0}, @IsDeleted = {1}, @msg = {2} OUTPUT",
                id, true, msgParam
            );

            if (rowsAffected == 0)
                return NotFound(new { message = "Attachment not found." });

            string msg = msgParam.Value?.ToString() ?? "";
            return Ok(new { message = msg });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the attachment.", error = ex.Message });
        }
    }
}