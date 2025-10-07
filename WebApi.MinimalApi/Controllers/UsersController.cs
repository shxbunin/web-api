using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using System.Web.Http.ModelBinding;
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[Produces("application/json", "application/xml")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    private readonly LinkGenerator linkGenerator;

    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
        this.linkGenerator = linkGenerator;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user != null)
        {
            return mapper.Map<UserDto>(user);
        }

        return NotFound();
    }

    [HttpHead("{userId}")]
    public ActionResult<UserDto> FindUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user != null)
            return Content("", "application/json; charset=utf-8");
        return NotFound();
    }

    [HttpPost]
    public IActionResult CreateUser([FromBody] CreateUserDto user)
    {
        if (user is not null)
        {
            if (user.Login == null || user.Login.Any(x => !char.IsLetterOrDigit(x)))
            {
                ModelState.AddModelError("Login", "Недопустимые символы");
            }

            if (!ModelState.IsValid)
            {
                return UnprocessableEntity(ModelState);
            }

            var userEntity = userRepository.Insert(mapper.Map<UserEntity>(user));

            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = userEntity.Id },
                userEntity.Id);
        }
        else
        {
            return BadRequest();
        }
    }

    [HttpPut("{userId}")]
    public IActionResult FullUpdateUser([FromRoute] string userId, [FromBody] FullUpdateUserDto user)
    {
        Guid userGuid;
        var isGuidValid = Guid.TryParse(userId, out userGuid);

        if (user is not null && isGuidValid)
        {
            if (!ModelState.IsValid)
            {
                return UnprocessableEntity(ModelState);
            }

            user.Id = userGuid;

            bool isInserted;

            userRepository.UpdateOrInsert(mapper.Map<UserEntity>(user), out isInserted);

            if (isInserted)
            {
                return CreatedAtRoute(
                    nameof(GetUserById),
                    new { userId },
                    userId);
                
            }
            else
            {
                return NoContent();
            }
        }
        else
        {
            return BadRequest();
        }
    }

    [HttpDelete("{userId}")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user != null)
        {
            userRepository.Delete(userId);
            return NoContent();
        }

        return NotFound();
    }

    [HttpGet(Name = "GetUsers")]
    public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var pageList = userRepository.GetPage(pageNumber, pageSize);
        var paginationHeader = new
        {
            previousPageLink = pageList.HasPrevious
                ? linkGenerator.GetUriByRouteValues(HttpContext, "GetUsers", new { pageNumber = pageNumber - 1, pageSize })
                : null,
            nextPageLink = pageList.HasNext
                ? linkGenerator.GetUriByRouteValues(HttpContext, "GetUsers", new { pageNumber = pageNumber + 1, pageSize })
                : null,
            totalCount = pageList.TotalCount,
            pageSize = pageSize < 1 ? 1 : (pageSize > 20 ? 20 : pageSize),
            currentPage = pageNumber < 1 ? 1 : pageNumber,
            totalPages = pageList.TotalPages,
        };

        Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
        var users = mapper.Map<IEnumerable<UserDto>>(pageList);

        return Ok(users);
    }

    [HttpOptions]
    public IActionResult OptionsUsers()
    { 
        Response.Headers.Add("Allow", "POST, GET, OPTIONS");
        return Ok();
    }
}