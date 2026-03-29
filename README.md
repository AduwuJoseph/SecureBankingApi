# 🏦 Banking API

A secure, production-grade RESTful Banking API built with **.NET 8**, **Entity Framework Core**, and **MySQL**.
This system supports user authentication, account management, and **financially safe fund transfers** with strong consistency guarantees.

---

## 🚀 Features

* 🔐 JWT Authentication & Authorization
* 👤 User Registration & Login
* 💳 Account Management
* 💸 Fund Transfers (Atomic & Idempotent)
* 📜 Transaction History
* 🛡️ Global Exception Handling
* 🔁 Idempotency Support (Prevents Duplicate Transfers)
* 🔒 Concurrency Control (Row-level locking)
* 📊 Swagger API Documentation

---

## 🏗️ Architecture

This project follows **Clean Architecture** principles:

```
Domain → Application → Infrastructure → API
```

* **Domain**: Core business entities
* **Application**: Business logic and services
* **Infrastructure**: Database, repositories, external services
* **API**: Controllers and request handling

---

## ⚙️ Tech Stack

* .NET 8 Web API
* Entity Framework Core
* MySQL (Pomelo Provider)
* JWT Authentication
* Swagger (OpenAPI)
* xUnit (Testing)

---

## 📦 Prerequisites

Ensure you have the following installed:

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [MySQL Server](https://dev.mysql.com/downloads/)
* [Git](https://git-scm.com/)
* (Optional) Postman / Swagger UI

---

## 🛠️ Setup Instructions

### 1. Clone the Repository

```bash
git clone https://github.com/AduwuJoseph/SecureBankingApi.git
cd banking-api
```

---

### 2. Configure Environment

Update your `appsettings.json`:

```json
"ConnectionStrings": {
  "Default": "server=localhost;port=3306;database=banking_db;user=root;password=yourpassword;"
}
```

---

### 3. Run Database Migrations

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

> This will create all required tables (Users, Accounts, Transactions, etc.)

---

### 4. Run the Application

```bash
dotnet run
```

The API will start at:

```
https://localhost:5001
http://localhost:5000
```

---

### 5. Access Swagger UI

Open in browser:

```
https://localhost:5001/swagger
```

---

## 🔑 Authentication Flow

1. Register a user:

   ```
   POST /api/auth/register
   ```

2. Login:

   ```
   POST /api/auth/login
   ```

3. Copy JWT token from response

4. Authorize in Swagger:

   ```
   Bearer <your-token>
   ```

---

## 💸 Fund Transfer (Important)

### Endpoint:

```
POST /api/transactions/transfer
```

### Headers:

```
Authorization: Bearer <token>
Idempotency-Key: unique-key-123
```

### Why Idempotency?

Ensures that **repeated requests do not result in duplicate debit transactions**, especially in cases of network retries.

---

## 🧪 Running Tests

```bash
dotnet test
```

---

## 🛡️ Security Considerations

* Passwords are hashed using BCrypt
* JWT-based authentication protects endpoints
* Input validation via Data Annotations / Validators
* Global exception handling prevents sensitive data leaks

---

## 📈 Design Decisions

* **Atomic Transactions**: Ensures money is never lost or duplicated
* **Database Locking**: Prevents race conditions during transfers
* **Idempotency**: Guarantees safe retries
* **Clean Architecture**: Improves maintainability and scalability

---

## ⚠️ Trade-offs

* Added complexity (locking, idempotency) for higher reliability
* Slight latency during transfers due to transaction safety

---

## 🔮 Future Improvements

* Redis-based distributed locking
* Event-driven architecture (Kafka/RabbitMQ)
* Fraud detection system
* Rate limiting & API throttling
* Multi-currency support

---

## 👨‍💻 Author

Joseph

---

## 📄 License

This project is for technical assessment/demo purposes.
