using BankingAPI.API.Middleware;
using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BankingAPI.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TransferController : ControllerBase
    {
        private readonly ITransferService _transferService;
        private readonly ILogger<TransferController> _logger;

        public TransferController(ITransferService transferService, ILogger<TransferController> logger)
        {
            _transferService = transferService;
            _logger = logger;
        }

        [HttpPost]
        [RateLimit(PeriodInSeconds = 60, Limit = 5)] // Max 5 transfers per minute
        public async Task<IActionResult> Transfer([FromBody] TransferRequest transferRequest)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            try
            {
                var result = await _transferService.TransferAsync(userId, transferRequest);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ConcurrencyException ex)
            {
                return Conflict(new { error = ex.Message, retryable = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer failed for user {UserId}", userId);
                return StatusCode(500, new { error = "Transfer failed. Please try again." });
            }
        }

        [HttpGet("status/{reference}")]
        public async Task<IActionResult> GetTransactionStatus(string reference)
        {
            try
            {
                var result = await _transferService.GetTransactionStatusAsync(reference);
                return Ok(result);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }
    }
}