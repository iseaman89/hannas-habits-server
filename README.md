# Hanna's Habits – Backend 🧠

This is the **backend** of the fullstack project *Hanna’s Habits* – a habit-tracking and journaling web app built with .NET and React.

🔗 **Frontend repository**: [hannas-habits-frontend](https://github.com/iseaman89/hannas-habits-ui)  
🔗 **Main project overview**: [hannas-habits](https://github.com/iseaman89/hannas-habits)

---

## ⚙️ Stack

- ASP.NET Core 8 (Web API)
- Entity Framework Core
- PostgreSQL
- JWT Authentication
- Swagger for API documentation
- Docker support

---

## 🚀 Running the API

### 🧪 Local development

#### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- PostgreSQL (local or via Docker)

#### Steps

```bash
git clone https://github.com/iseaman89/hannas-habits-server.git
cd hannas-habits-server

# Set up your connection string and JWT secret in appsettings.json or environment variables

dotnet ef database update     # apply migrations
dotnet run                    # run the API
```

Access Swagger UI at:  
👉 `http://localhost:5000/swagger` *(or configured port)*

---

### 🐳 With Docker

```bash
docker-compose up --build
```

*Ensure your `docker-compose.yml` contains PostgreSQL and backend services with proper ports and env variables.*

---

## 🧰 Features

- ✅ Secure JWT-based auth with refresh tokens
- ✅ Custom user roles and access protection
- ✅ Habit creation, update, delete per user
- ✅ Journaling with timestamps
- ✅ Swagger UI for testing endpoints

---

## 📁 Project Structure

```
/Controllers      → API endpoints
/Models           → Entity & DTO classes
/Data             → EF DbContext & migrations
/Services         → Business logic & dependency injection
/Helpers          → JWT utils, settings
```

---

## 🧑‍💻 Author

**Yevgen Panych** – Umschüler zum Fachinformatiker AE  

📫 [LinkedIn](https://www.linkedin.com/in/yevgen-panych)  
🌐 [Portfolio](https://panych.site)
