using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Tests.Unit
{
    public class TransferServiceTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMemoryCache> _cacheMock;
        private readonly Mock<ILogger<TransferService>> _loggerMock;
        private readonly Mock<ITransactionValidator> _validatorMock;
        private readonly TransferService _service;

        public TransferServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _cacheMock = new Mock<IMemoryCache>();
            _loggerMock = new Mock<ILogger<TransferService>>();
            _validatorMock = new Mock<ITransactionValidator>();

            _service = new TransferService(
                _unitOfWorkMock.Object,
                _cacheMock.Object,
                _loggerMock.Object,
                _validatorMock.Object);
        }

        [Fact]
        public async Task TransferAsync_ValidTransfer_ReturnsTransactionDto()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var transferDto = new TransferDto
            {
                RecipientAccountNumber = "ACC20231201123456",
                Amount = 100,
                Description = "Test transfer",
                IdempotentKey = "unique_key_123"
            };

            var senderAccount = new Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = "ACC20231201123455",
                Balance = 1000,
                IsActive = true,
                Currency = "USD"
            };

            var recipientAccount = new Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = "ACC20231201123456",
                Balance = 500,
                IsActive = true,
                Currency = "USD"
            };

            _unitOfWorkMock.Setup(x => x.Transactions.GetByIdempotentKeyAsync(It.IsAny<string>()))
                .ReturnsAsync((Transaction)null);

            _unitOfWorkMock.Setup(x => x.Accounts.GetAccountWithUserAsync(userId))
                .ReturnsAsync(senderAccount);

            _unitOfWorkMock.Setup(x => x.Accounts.GetByAccountNumberAsync(transferDto.RecipientAccountNumber))
                .ReturnsAsync(recipientAccount);

            _validatorMock.Setup(x => x.ValidateTransferAsync(senderAccount,
                transferDto.RecipientAccountNumber, transferDto.Amount))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.Accounts.UpdateBalanceAsync(
                senderAccount.Id, It.IsAny<decimal>(), It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            _unitOfWorkMock.Setup(x => x.Accounts.UpdateBalanceAsync(
                recipientAccount.Id, It.IsAny<decimal>(), It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            _unitOfWorkMock.Setup(x => x.CompleteAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _service.TransferAsync(userId, transferDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(transferDto.Amount, result.Amount);
            Assert.Equal(transferDto.Description, result.Description);
            Assert.Equal("Completed", result.Status);

            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task TransferAsync_InsufficientFunds_ThrowsException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var transferDto = new TransferDto
            {
                RecipientAccountNumber = "ACC20231201123456",
                Amount = 2000,
                Description = "Test transfer",
                IdempotentKey = "unique_key_456"
            };

            var senderAccount = new Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = "ACC20231201123455",
                Balance = 1000,
                IsActive = true,
                Currency = "USD"
            };

            _unitOfWorkMock.Setup(x => x.Transactions.GetByIdempotentKeyAsync(It.IsAny<string>()))
                .ReturnsAsync((Transaction)null);

            _unitOfWorkMock.Setup(x => x.Accounts.GetAccountWithUserAsync(userId))
                .ReturnsAsync(senderAccount);

            _validatorMock.Setup(x => x.ValidateTransferAsync(senderAccount,
                transferDto.RecipientAccountNumber, transferDto.Amount))
                .ThrowsAsync(new InsufficientFundsException("Insufficient funds"));

            // Act & Assert
            await Assert.ThrowsAsync<InsufficientFundsException>(() =>
                _service.TransferAsync(userId, transferDto));

            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Never);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task TransferAsync_DuplicateIdempotentKey_ReturnsExistingTransaction()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var transferDto = new TransferDto
            {
                RecipientAccountNumber = "ACC20231201123456",
                Amount = 100,
                Description = "Test transfer",
                IdempotentKey = "duplicate_key"
            };

            var existingTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                TransactionReference = "TXN20231201123456",
                Amount = 100,
                Status = TransactionStatus.Completed,
                InitiatedAt = DateTime.UtcNow
            };

            _unitOfWorkMock.Setup(x => x.Transactions.GetByIdempotentKeyAsync(transferDto.IdempotentKey))
                .ReturnsAsync(existingTransaction);

            // Act
            var result = await _service.TransferAsync(userId, transferDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(existingTransaction.TransactionReference, result.TransactionReference);
            Assert.Equal(existingTransaction.Amount, result.Amount);

            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Never);
            _unitOfWorkMock.Verify(x => x.Accounts.GetAccountWithUserAsync(It.IsAny<Guid>()), Times.Never);
        }
    }
}
