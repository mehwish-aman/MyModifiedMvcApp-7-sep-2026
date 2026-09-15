using Microsoft.AspNetCore.Mvc; //enabling mvc services
using Microsoft.Data.SqlClient;
using System.Data;   //for direction parameter for id which we used in insert func
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;   // ye naya add kiya

public class InventoryController : Controller //inheriting render, req features etc
{ 

                                            //Constructor depandency injection
     //Ye poora code sirf ek "database connection ka setup" hai, taake har method ke andar _context.InventoryItems...
     //likh ke hum database se data le/de sakein, begair baar baar naya connection banaye.

    private readonly AppDbContext _context;   //Ye sirf keh raha hai: "Is Controller ke paas ek _context naam ki cheez hogi, 
    //jo database se baat karegi." Abhi khali hai, sirf declare kiya.
    private const string CurrentUser = "user";
    public InventoryController(AppDbContext context)  //Jab bhi website kholtengy ho (Inventory page pe jaty hain), 
    //ASP.NET khud ek AppDbContext bana kar is Controller ko de deta hai khud kuch karna nahi padta, ye automatic hai.
    {
        _context = context;  //Jo cheez ASP.NET ne di, use humne apne _context field mein save kar liya, taake niche jitne 
        //bhi methods hain (Add, Save, Delete), sab isko use kar sakein database se baat karne ke liye.
    }



///////TRANSACTION BLOCK/////

[HttpPost]
public async Task<IActionResult> SaveWithAttachments(
    [FromForm] InventoryItem model,
    List<IFormFile> newFiles,
    List<int> removedAttachmentIds)
{
    if (!ModelState.IsValid)
        return BadRequest(new { message = "Validation failed. Please check field limits." });

    var strategy = _context.Database.CreateExecutionStrategy();

    try
    {
        var result = await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ---- Step 1: Insert or Update the item ----
                var idParam = new SqlParameter("@Id", SqlDbType.Int)
                {
                    Direction = ParameterDirection.InputOutput,
                    Value = model.Id > 0 ? model.Id : (object)DBNull.Value
                };
                var itemMsg = new SqlParameter("@msg", SqlDbType.NVarChar, 250)
                {
                    Direction = ParameterDirection.Output
                };

                _context.Database.ExecuteSqlRaw(
                    "EXEC Inventory_Items @Id = {0} OUTPUT, @ItemName = {1}, @Description = {2}, @PurchaseValue = {3}, @PurchaseDate = {4}, @Tax = {5}, @Company = {6}, @Total = {7}, @CategoryId = {8}, @AddedBy = {9}, @msg = {10} OUTPUT",
                    idParam, model.ItemName, model.Description, model.PurchaseValue, model.PurchaseDate,
                    model.Tax, model.Company, model.Total, model.CategoryId, CurrentUser, itemMsg);

                ThrowIfSpError(itemMsg, "Item save");

                int itemId = idParam.Value == DBNull.Value ? model.Id : (int)idParam.Value;

                // ---- Step 2: soft-delete attachments the user removed ----
                foreach (var removedId in removedAttachmentIds ?? new List<int>())
                {
                    var delMsg = new SqlParameter("@msg", SqlDbType.NVarChar, 250) { Direction = ParameterDirection.Output };
                    _context.Database.ExecuteSqlRaw(
                        "EXEC Attachments_Manage @Id = {0}, @IsDeleted = {1}, @ModifiedBy = {2}, @msg = {3} OUTPUT",
                        removedId, true, CurrentUser, delMsg);
                    ThrowIfSpError(delMsg, $"Attachment delete (Id={removedId})");
                }

                // ---- Step 3: insert newly staged files ----
                foreach (var file in newFiles ?? new List<IFormFile>())
                {
                   // throw new Exception("Testing rollback manually");
                   //throw new Exception("Testing update rollback");   // TEMP
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var fileData = ms.ToArray();

                    var insMsg = new SqlParameter("@msg", SqlDbType.NVarChar, 250) { Direction = ParameterDirection.Output };
                    _context.Database.ExecuteSqlRaw(
                        "EXEC Attachments_Manage @InventoryItemId = {0}, @FileName = {1}, @FileType = {2}, @FileData = {3}, @CreatedBy = {4}, @msg = {5} OUTPUT",
                        itemId, file.FileName, file.ContentType, fileData, CurrentUser, insMsg);
                    ThrowIfSpError(insMsg, $"Attachment upload ({file.FileName})");
                }

                transaction.Commit();

                return new
                {
                    message = "Item saved successfully with attachments",
                    id = itemId,
                    itemName = model.ItemName,
                    description = model.Description,
                    purchaseValue = model.PurchaseValue,
                    purchaseDate = model.PurchaseDate.ToShortDateString(),
                    rawDate = model.PurchaseDate.ToString("yyyy-MM-dd"),
                    tax = model.Tax,
                    total = model.Total,
                    company = model.Company,
                    categoryId = model.CategoryId
                };
            }
            catch
            {
                transaction.Rollback();   // item insert, attachment deletes, attachment inserts — ALL undone
                throw;
            }
        });

        return Ok(result);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = "Save failed — no changes were made.", error = ex.Message });
    }
}

private static void ThrowIfSpError(SqlParameter msgParam, string context)
{
    var msg = msgParam.Value as string;
    if (!string.IsNullOrEmpty(msg) && msg.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"{context} failed: {msg}");
}







                                                    //Add/GET method  for items through SP calling
     //  will handle the GET request to display the inventory items page.
     //  It will fetch the inventory items from the database and pass them to the view.
     //flow
     //when inventory->add url on loading page, br sends get req to server, asp ka routing sys dekhta hai 
     //get req ae hai r httpget point krta hai ky yeh method is req ko handle kryga. iisi liye asp yhnin rukeyga.
     [HttpGet]
     //FOR SHOWING LIST OF ITEMS FROM DATABASE THROUGH SP CALL-FETCHING ITEMS FROM DATABASE
     
    public IActionResult Add()
    {  

        try
        {
            var msgParam=new SqlParameter("@msg", System.Data.SqlDbType.NVarChar, 250)
            {
                Direction=ParameterDirection.Output
            };
            var items = _context.InventoryItems //context db ka constructor hai,inventory items uski property hai
            //jo table ko rep kr rahi hai takay hm data jo db sy a raha usy bd mein use kr skein jesy hmny view 
            //mein dikhan ahai nahin kriengy to b chlyga pr ap inspect nhi r skty phr is liye bnay ahai.
            .FromSqlRaw("EXEC Inventory_Items @msg= {0} OUTPUT", msgParam)  //sp yahn call kiya hai yeh from wali data get krny ky liye use hota hai
            //0 a placeholder whihc is used to save value safely  and select yeh sp  hai to get data from db
            .AsEnumerable()      //error ki waja sy dala tha ye is sy resolve ho gaya tha(that ef error on decs now it will tell to conv data into c#list and handle)
            .OrderByDescending(i=>i.Id)
            .ToList(); //to be safe from multiple data query or connection closed error
            return View(items); //yhn view return ho jayga yani jis ki get req ae hai, html mein model list item declared hai yeh render krdega using that @foreach loop into html view page
        }
        catch (Exception ex)
        {
            // Agar koi exception aata hai, to 500 Internal Server Error ke saath error message bhej do
            return StatusCode(500, new { message = "An error occurred while fetching the items.", error = ex.Message });
        }
    }


//get method for Categories through SP calling
    [HttpGet]

    public IActionResult GetCategories()
    {
        try
        {
            var categories = _context.Categories
                .FromSqlRaw("EXEC Category_get")
                .AsEnumerable()
                .Select(c => new { c.Id, c.Name })
                .ToList();

            return Ok(categories); // Return the categories as JSON
        }
        catch (Exception ex)
        {
            // Agar koi exception aata hai, to 500 Internal Server Error ke saath error message bhej do
            return StatusCode(500, new { message = "An error occurred while fetching the categories.", error = ex.Message });
            
        }
    }



                                                    //Save
//Save method will handle the POST request to save the inventory item to the database
//when user add data in form and click save btn , js ky fetch loggic say req jayegi post req.
//Browser sy fetch req ae having formdata. asp.net us data ka inventoryitem model  ka obj bana dega, name ko name mein, 
//tax ko tex mein(model binding)
//ye sb  method chalny sy pehly hi ho jata hai.
     /*   [HttpPost]

    //FOR INSERTING ITEM INTO DATABASE USING SP CALL
    public IActionResult Save(InventoryItem model)
    {    //validation, checks if data has any invalid value like not in range etc then ye error msg shw kryga

        //if not valid 405 error
        if (!ModelState.IsValid)
    {   //badRequest http ka 400 status code ky sath error bhjeta hai,for val rules on char limit and digit range
        return BadRequest(new { message = "Validation failed. Please check field limits." });
    }
    try
        {
            
      //jo id ki value yhn sy jaygi wo db mein ja kr new id ky sath wapis ayegi. us id ko leney ky liye yeh new parameter sy var bnaya hai takay pata chalay C# ko ky iski value
      //srf sp ko jaygi nhin blky wahan sy new value wapis b aygi is liye isko output ki direction mein rkaha hai
      //output not declare rror sy bachny ky liye,
    var idParam =new SqlParameter("@Id", System.Data.SqlDbType.Int)
    {
        Direction= ParameterDirection.Output
    };
    var msgParam=new SqlParameter("@msg", System.Data.SqlDbType.NVarChar, 250)
    {
        Direction=ParameterDirection.Output
    };
    //EXECUUTE wali jab srf query chalani ho, data wapis na chhaiyay ho. r ye value {n}, sql injection sy bachny ky liye.
    _context.Database.ExecuteSqlRaw(
    "EXEC Inventory_Items @Id = {0} OUTPUT, @ItemName = {1}, @Description = {2}, @PurchaseValue = {3}, @PurchaseDate = {4}, @Tax = {5}, @Company = {6}, @Total = {7}, @CategoryId = {8}, @AddedBy = {9},@msg = {9} OUTPUT",
    idParam, model.ItemName, model.Description, model.PurchaseValue, model.PurchaseDate, model.Tax, model.Company, model.Total, model.CategoryId, CurrentUser, msgParam
);
     model.Id = (int)idParam.Value;   // database se naya generated Id yahan mil gaya. yeh ab model ky thrgh view mwin dikha sakty hain hm ab. pr isy pehly cnvrt kr liya hai int mein.
     string msg = msgParam.Value?.ToString() ?? " ";  // database se msg wapis mil gaya. yeh ab model ky thrgh view mwin dikha sakty hain hm ab. pr isy pehly cnvrt kr liya hai string mein.
         //new- http- 200 okay status code. aik anonymous obj mein srf data hota hai auto con`vert json mein ho kr phir 
         //S ko milta hai new item ki sari details. JS us data ko table mein show krta hai.
    return Ok(new
    {   //ye msg json mein cnvrt hokr JS(frntend)ko wapis jata hai. kay sb okay howa.JS begair pg relod kiyay naya row add kr deta hai table mein.
       message = msg,
        id = model.Id,
        itemName = model.ItemName,
        description = model.Description,
        purchaseValue = model.PurchaseValue,
        purchaseDate = model.PurchaseDate.ToShortDateString(),
        rawDate = model.PurchaseDate.ToString("yyyy-MM-dd"),
        tax = model.Tax,
        total = model.Total,
        company = model.Company,
        categoryId = model.CategoryId
    });
        }
    catch (Exception ex)
        {
            // Agar koi exception aata hai, to 500 Internal Server Error ke saath error message bhej do
            return StatusCode(500, new { message = "An error occurred while saving the item.", error = ex.Message });
        }
}
                                                    //Update Method
//Update method mein Id URL se aati hai (/Inventory/Update/7 → id=7), aur baaki form data request body se aata hai 
// dono alag jagah se, ek hi method mein milte hain. Find(id) se purani row database se nikal ke uski properties naye 
// data se manually replace ki jati hain (Id/IsDeleted chhod ke). SaveChanges() call hote hi EF Core khud samajh jata hai 
// ye ek UPDATE hai (na ke naya INSERT), kyunki row already "tracked" thi.

 [HttpPost]
[Route("Inventory/Update/{id}")]
public IActionResult Update(int id, InventoryItem model)
{   

    try
    {
    var msgParam=new SqlParameter("@msg", System.Data.SqlDbType.NVarChar, 250)
        {
            Direction=ParameterDirection.Output
        };
        var rowsAffected = _context.Database.ExecuteSqlRaw(
    "EXEC Inventory_Items @Id = {0}, @ItemName = {1}, @Description = {2}, @PurchaseValue = {3}, @PurchaseDate = {4}, @Tax = {5}, @Company = {6}, @Total = {7}, @CategoryId = {8}, @msg = {9} OUTPUT",
    id, model.ItemName, model.Description, model.PurchaseValue, model.PurchaseDate, model.Tax, model.Company, model.Total, model.CategoryId, msgParam
);
        if (rowsAffected == 0)
        {
            return NotFound(new { message = "Item not found." });
        }
        string msg = msgParam.Value?.ToString() ?? " ";  
        return Ok(new
        {
            message = msg,
            id = model.Id,
            itemName = model.ItemName,
            description = model.Description,
            purchaseValue = model.PurchaseValue,
            purchaseDate = model.PurchaseDate.ToShortDateString(),
            tax = model.Tax,
            rawDate = model.PurchaseDate.ToString("yyyy-MM-dd"),
            total = model.Total,
            company = model.Company,
            categoryId = model.CategoryId
        });
    }
    catch (Exception ex)
    {
        // Agar koi exception aata hai, to 500 Internal Server Error ke saath error message bhej do
        return StatusCode(500, new { message = "An error occurred while updating the item.", error = ex.Message });
    }
}*/

                                                /// Delete Method
    //URL se id leke Find(id) se row dhoondhi jati hai,
    //  IsDeleted = true set kiya jata hai (row delete nahi hoti, bas flag change hota hai), 
    // SaveChanges() se UPDATE query chalti hai, aur response mein sirf success message bheja jata hai 
    // kyunki row hatana JavaScript khud DOM se kar leti hai.   

    [HttpPost]
    [Route("Inventory/Delete/{id}")]
public IActionResult Delete(int id)
{
    try
    {
            var msgParam = new SqlParameter("@msg", System.Data.SqlDbType.NVarChar, 250)
        {
            Direction = ParameterDirection.Output
        };
         var rowsAffected = _context.Database.ExecuteSqlRaw(
            "EXEC Inventory_Items @Id = {0}, @IsDeleted = {1}, @msg = {2} OUTPUT",
            id, true, msgParam
        );
        if (rowsAffected==0)
        {
            return NotFound(new { message = "Item not found." });
        }
       // string msg =msgParam.Value?.ToString() ?? "Item deleted Successfully.";
        return Ok(new { message = msgParam.Value?.ToString() ?? " "});

   }
   catch (Exception ex)
    {
        // Agar koi exception aata hai, to 500 Internal Server Error ke saath error message bhej do
        return StatusCode(500, new { message = "An error occurred while deleting the item.", error = ex.Message });
    }
}

}
// =====================================================
// CRUD FLOW SUMMARY--- SP-based Inventory Controller
// =====================================================
// Method   | HTTP Verb | Data kahan se aata hai              | EF method                      | Response mein kya
// ---------|-----------|--------------------------------------|---------------------------------|----------------------------
// List     | GET       | Kahin se nahi (bas fetch)            | FromSqlRaw + AsEnumerable       | Poori list (View ko)
// Save     | POST      | Form body (Model Binding)            | ExecuteSqlRaw + OUTPUT param    | Naya item + naya Id
// Update   | POST      | URL (id) + Form body (model)         | ExecuteSqlRaw                   | Updated item confirmation
// Delete   | POST      | URL (id) sirf                        | ExecuteSqlRaw                   | Sirf success message
// ===========================================================================================================================t