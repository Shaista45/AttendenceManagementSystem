using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class ApiAuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public ApiAuthController(IConfiguration configuration) { _configuration = configuration; }

    [HttpPost("token")]
    public IActionResult GetToken()
    {
        // Simple JWT Generation Logic
        var claims = new[] { new System.Security.Claims.Claim("username", "admin") };
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            _configuration["Jwt:Issuer"],
            _configuration["Jwt:Audience"],
            claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: creds
        );

        return Ok(new { token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token) });
    }
}