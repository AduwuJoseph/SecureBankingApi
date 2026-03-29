using FluentAssertions;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Domain.Enum;

namespace BankingAPI.UnitTests.DTOs;

public class TransactionViewModelTests
{
    [Fact]
    public void TransactionViewModel_ShouldFormatAmountCorrectly()
    {
        // Arrange
        var viewModel = new TransactionViewModel
        {
            Amount = 1234.56M
        };

        // Act & Assert
        viewModel.FormattedAmount.Should().Be("$1,234.56");
    }

    [Fact]
    public void TransactionViewModel_ShouldFormatDateCorrectly()
    {
        // Arrange
        var date = new DateTime(2024, 1, 15, 14, 30, 0);
        var viewModel = new TransactionViewModel
        {
            Timestamp = date
        };

        // Act & Assert
        viewModel.FormattedDate.Should().Be("Jan 15, 2024");
        viewModel.FormattedTime.Should().Be("02:30 PM");
    }

    [Fact]
    public void CounterpartyInfo_ShouldGenerateCorrectInitials()
    {
        // Arrange & Act
        var singleName = new CounterpartyInfo { Name = "John" };
        var twoNames = new CounterpartyInfo { Name = "John Doe" };
        var threeNames = new CounterpartyInfo { Name = "John Michael Doe" };
        var emptyName = new CounterpartyInfo { Name = "" };
        var nullName = new CounterpartyInfo { Name = null! };

        // Assert
        singleName.Initials.Should().Be("JO");
        twoNames.Initials.Should().Be("JD");
        threeNames.Initials.Should().Be("JD");
        emptyName.Initials.Should().Be("??");
        nullName.Initials.Should().Be("??");
    }

    [Theory]
    [InlineData(30, "Just now")]
    [InlineData(90, "1 minute ago")]
    [InlineData(150, "2 minutes ago")]
    [InlineData(3600, "1 hour ago")]
    [InlineData(7200, "2 hours ago")]
    [InlineData(86400, "1 day ago")]
    [InlineData(172800, "2 days ago")]
    [InlineData(604800, "1 week ago")]
    [InlineData(1209600, "2 weeks ago")]
    public void TransactionViewModel_ShouldCalculateRelativeTimeCorrectly(int secondsAgo, string expected)
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddSeconds(-secondsAgo);
        var viewModel = new TransactionViewModel
        {
            Timestamp = timestamp
        };

        // Act
        var relativeTime = viewModel.RelativeTime;

        // Assert
        relativeTime.Should().Be(expected);
    }

    [Fact]
    public void TransactionHistoryResponseDto_ShouldCalculatePaginationCorrectly()
    {
        // Arrange
        var response = new TransactionHistoryResponse
        {
            Pagination = new PaginationMetadata
            {
                CurrentPage = 2,
                PageSize = 10,
                TotalCount = 95,
                TotalPages = 10
            }
        };

        // Act & Assert
        response.Pagination.HasPrevious.Should().BeTrue();
        response.Pagination.HasNext.Should().BeTrue();
    }

    [Fact]
    public void TransactionSummary_ShouldCalculateNetFlowCorrectly()
    {
        // Arrange
        var summary = new TransactionSummary
        {
            TotalSent = 1000,
            TotalReceived = 1500
        };

        // Act & Assert
        summary.NetFlow.Should().Be(500);
        summary.FormattedNetFlow.Should().Be("+$500.00");
    }

    [Fact]
    public void TransactionSummary_ShouldFormatNegativeNetFlowCorrectly()
    {
        // Arrange
        var summary = new TransactionSummary
        {
            TotalSent = 1500,
            TotalReceived = 1000
        };

        // Act & Assert
        summary.NetFlow.Should().Be(-500);
        summary.FormattedNetFlow.Should().Be("-$500.00");
    }
}