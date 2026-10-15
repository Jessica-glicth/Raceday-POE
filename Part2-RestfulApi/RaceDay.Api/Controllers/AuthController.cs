using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordService _passwords;

    public AuthController(RaceDayDbContext db, IPasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    /// <summary>Registers a new Organiser or Participant account and starts a session.</summary>
    /// <remarks>
    /// The role is chosen at registration and stored by which table the account is created in.
    /// Emails are unique across both roles. The password is hashed (PBKDF2 + salt) before storage.
    /// </remarks>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResponse>> Register(RegisterRequest request)
    {
        if (!Roles.TryParse(request.Role, out var role))
        {
            ModelState.AddModelError(nameof(request.Role), "Role must be 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var taken = await _db.Organisers.AnyAsync(o => o.Email == email)
                    || await _db.Participants.AnyAsync(p => p.Email == email);
        if (taken)
            return Conflict(new ProblemDetails { Title = "Email already registered", Status = StatusCodes.Status409Conflict });

        var hash = _passwords.Hash(request.Password);
        AccountResponse account;

        try
        {
            if (role == Roles.Organiser)
            {
                var organiser = new Organiser
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = email,
                    PasswordHash = hash,
                    ContactNumber = request.ContactNumber
                };
                _db.Organisers.Add(organiser);
                await _db.SaveChangesAsync();
                account = new AccountResponse(organiser.OrganiserID, role, organiser.FirstName, organiser.LastName, organiser.Email, organiser.ContactNumber);
            }
            else
            {
                var participant = new Participant
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = email,
                    PasswordHash = hash
                };
                _db.Participants.Add(participant);
                await _db.SaveChangesAsync();
                account = new AccountResponse(participant.ParticipantID, role, participant.FirstName, participant.LastName, participant.Email, null);
            }
        }
        catch (DbUpdateException)
        {
            // Unique index on Email caught a simultaneous registration.
            return Conflict(new ProblemDetails { Title = "Email already registered", Status = StatusCodes.Status409Conflict });
        }

        await SignInAsync(account);
        return StatusCode(StatusCodes.Status201Created, account);
    }

    /// <summary>Logs in with email and password and starts a session that remembers the user's role.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        AccountResponse? account = null;

        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.Email == email);
        if (organiser != null && _passwords.Verify(request.Password, organiser.PasswordHash))
        {
            account = new AccountResponse(organiser.OrganiserID, Roles.Organiser, organiser.FirstName, organiser.LastName, organiser.Email, organiser.ContactNumber);
        }
        else if (organiser == null)
        {
            var participant = await _db.Participants.FirstOrDefaultAsync(p => p.Email == email);
            if (participant != null && _passwords.Verify(request.Password, participant.PasswordHash))
                account = new AccountResponse(participant.ParticipantID, Roles.Participant, participant.FirstName, participant.LastName, participant.Email, null);
        }

        if (account == null)
            return Unauthorized(new ProblemDetails { Title = "Invalid email or password", Status = StatusCodes.Status401Unauthorized });

        await SignInAsync(account);
        return Ok(account);
    }

    /// <summary>Ends the current session.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private async Task SignInAsync(AccountResponse account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.ID.ToString()),
            new(ClaimTypes.Name, $"{account.FirstName} {account.LastName}"),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, account.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
    }
}
