using BankingAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BankingAPI.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IAccountService accountService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountDetails()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            try
            {
                var account = await _accountService.GetAccountDetailsAsync(userId);
                return Ok(account);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            try
            {
                var balance = await _accountService.GetAccountBalanceAsync(userId);
                return Ok(new { balance });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactionHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            try
            {
                var transactions = await _accountService.GetTransactionHistoryAsync(
                    userId, page, pageSize);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction history for user {UserId}", userId);
                return StatusCode(500, new { error = "Failed to retrieve transaction history" });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAccountInfo([FromBody] UpdateAccountDto updateDto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            try
            {
                var result = await _accountService.UpdateAccountInfoAsync(userId, updateDto.PhoneNumber);
                return Ok(new { success = result });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }
    }
}