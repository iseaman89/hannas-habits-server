
# Hanna's Habits – Backend (Microservices) 🧠

This is the **backend** of the fullstack project *Hanna’s Habits* – structured as a microservice architecture using .NET Core.

It contains two independent ASP.NET Core Web API services:

| Service           | Description                                          |
|------------------|------------------------------------------------------|
| 🧑‍💼 User Service     | Handles authentication, JWT tokens, and user management |
| 📘 Habits Service   | Manages habit tracking and journaling features     |

🔗 **Frontend repository**: [hannas-habits-frontend](https://github.com/iseaman89/hannas-habits-ui)  
🔗 **Main project overview**: [hannas-habits](https://github.com/iseaman89/hannas-habits)

---

## 🧱 Project Structure

```
/user-service         → ASP.NET Core service for user management & auth
/habits-service       → ASP.NET Core service for habits & journals
```

---

## ⚙️ Technologies Used

- ASP.NET Core 7 (Web API)
- Entity Framework Core
- PostgreSQL (separate DB per service)
- JWT Authentication
- Swagger (enabled in both services)
- Docker & Docker Compose

---

## 🚀 Running Locally

### 🐳 Using Docker Compose (Recommended)

```bash
docker-compose up --build
```

This will:
- Build and run both services (`user-service`, `habits-service`)
- Start PostgreSQL containers for each service
- Expose APIs on different ports (configure in `docker-compose.yml`)

Once running:

- User Service Swagger: [http://localhost:5001/swagger](http://localhost:5001/swagger)
- Habits Service Swagger: [http://localhost:5002/swagger](http://localhost:5002/swagger)

> ⚠️ Make sure ports and environment variables match the ones defined in each service's `appsettings.json` or `.env` file.

---

## 🧪 Manual Run

You can also run each service separately:

```bash
cd user-service
dotnet ef database update
dotnet run
```

```bash
cd habits-service
dotnet ef database update
dotnet run
```

---

## 🔐 Authentication

- JWT-based auth is implemented in the **User Service**
- The **Habits Service** expects a valid JWT token in requests
- Token validation middleware is used for protected routes

---

## 📁 Each Service Contains:

```
/Controllers      → API endpoints
/Models           → Entity and DTO definitions
/Data             → EF DbContext & Migrations
/Services         → Core business logic
/Helpers          → Utility classes (e.g., JWT, config)
```

---

## 🧑‍💻 Author

**Yevgen Panych** – Umschüler zum Fachinformatiker AE

📫 [LinkedIn](https://www.linkedin.com/in/yevgen-panych)  
🌐 [Portfolio](https://panych.site)
