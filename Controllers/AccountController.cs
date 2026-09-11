using Microsoft.AspNetCore.Mvc;
public class AccountController : Controller  //controller ki class sy inherit kr liya hai account ny 
{
    //when app run ho to browser server ko get req bhejey and responce mein wo login page ka view bhej dy
    [HttpGet]
        public IActionResult Login()
    {
        return View();  ///first time jahna route hoga page lohin ka to data get kro means get ki request jayegi
    }

    [HttpPost] ////browser server kpo post request bhejy ga , server wo data login model
    //mein ja kar validsate kryga and if true to add inventory py  route kr dega 
    // wrna error msg show krygad
    public IActionResult Login(LoginModel model)
    {
        //checked for values login
        if(model.Username=="user"&& model.Password=="122")  //hardcoded credentials get kiyay huye data sy match kryga 
        {
            return RedirectToAction("Add","Inventory");  ///if matched then inventory ocntroller ky add action py route kr do
        }
        else
        ViewBag.error="Invalid Username or password";  //if not to yeh view bag error show kryga sath hi view refresh kr dega mean
        //wapis login py a jayga
        return View();
    }
}
