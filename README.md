# Bus System (ASP.NET Core Microservices)

Bus menu management system with a passenger-facing site and an admin portal built on ASP.NET Core microservices.

---

## Architecture Overview
- BusSystem.Web — passenger interface
- BusSystem.Admin.Web — admin panel
- BusSystem.Bus.API — bus management service
- BusSystem.Menu.API — menu & category service
- BusSystem.Identity.API — authentication & authorization
- BusSystem.FileStorage.API — image & file management

---

## Services & Ports
| Service | Port | Description |
|---|---|---|
| Bus.API | 5102 | Manages buses & QR codes |
| Menu.API | 5045 | Manages categories and menu items |
| Identity.API | 5271 | Authentication & authorization |
| FileStorage.API | 5002 | Image and file uploads |
| Admin.Web | 5012 | Admin dashboard |
| BusSystem.Web | 5155 | Passenger interface |

---

## Getting Started

### Prerequisites
- .NET 9 SDK
- SQL Server
- Visual Studio 2022 or VS Code

### Initial Configuration
Before running the project, you need to set up your configuration files:

1. Copy `appsettings.Example.json` to `appsettings.json` in each service/web app
2. Update the connection strings with your SQL Server instance
3. Ensure all services use the same JWT secret key
4. Configure email settings in Identity API (optional for password reset)

### Run Microservices
```powershell
cd src\Services\Bus\BusSystem.Bus.API
dotnet run

cd src\Services\Menu\BusSystem.Menu.API
dotnet run

cd src\Services\FileStorage\BusSystem.FileStorage.API
dotnet run

cd src\Services\Identity\BusSystem.Identity.API
dotnet run
```

### Run Web Applications
```powershell
cd src\WebApps\BusSystem.Admin.Web
dotnet run

cd src\WebApps\BusSystem.Web
dotnet run
```

---

## Application Access

### Passenger Interface
- Base URL: `http://localhost:5155/PlateNumber/{plateNumber}`
- Example: `http://localhost:5155/PlateNumber/34ABC123`
- Displays bus info, menu categories, and menu items with prices and images (mobile friendly).

### Admin Panel
- URL: `http://localhost:5012/`
- Features: bus management (QR generation), categories, menu items, image uploads, admin user management.

---

## API Endpoints

### Bus API
```
GET    /api/buses
GET    /api/buses/{id}
GET    /api/buses/plate/{plateNumber}
GET    /api/buses/{id}/exists
POST   /api/buses
POST   /api/buses/{id}/regenerate-qrcode
PUT    /api/buses/{id}
DELETE /api/buses/{id}
```

### Menu API
```
GET    /api/categories?busId={busId}
GET    /api/categories/{id}
GET    /api/categories/{id}/with-items
GET    /api/categories/bus/{busId}/with-items
POST   /api/categories
PUT    /api/categories/{id}
DELETE /api/categories/{id}

GET    /api/menuitems?categoryId={categoryId}
GET    /api/menuitems/{id}
POST   /api/menuitems
PUT    /api/menuitems/{id}
DELETE /api/menuitems/{id}
```

### Identity API
```
POST   /api/auth/login
GET    /api/auth/profile
PUT    /api/auth/profile
POST   /api/auth/change-password
POST   /api/auth/validate-token
POST   /api/auth/forgot-password
POST   /api/auth/validate-reset-token
POST   /api/auth/reset-password

GET    /api/users
GET    /api/users/{id}
POST   /api/users
PUT    /api/users/{id}
DELETE /api/users/{id}
```

### FileStorage API
```
POST   /api/files/upload
DELETE /api/files/delete
GET    /api/files/info
GET    /api/files/health
```

---

## Project Structure
```
src/
  Services/
    Bus/
    Menu/
    Identity/
    FileStorage/
  WebApps/
    BusSystem.Web
    BusSystem.Admin.Web
```

---

## Configuration

Example `appsettings.json` for Admin.Web:
```json
{
  "ApiUrls": {
    "BusService": "http://localhost:5102",
    "MenuService": "http://localhost:5045",
    "IdentityService": "http://localhost:5271",
    "FileStorageService": "http://localhost:5002"
  }
}
```

---

## Troubleshooting

- Ensure FileStorage.API is running for image uploads.
- Restart Admin.Web after configuration changes.
- Verify API URLs match running ports.

Health check:
```bash
curl http://localhost:5002/api/files/health
```

---

## Features
- ASP.NET Core microservices architecture
- RESTful APIs with CRUD operations
- QR code generation for buses
- Image upload support
- Authentication and admin management
- Responsive web UI

---

## Production Notes
- Do not commit secrets; use environment variables or secure config.
- Enable HTTPS and configure CORS.
- Add centralized logging/monitoring.

---

## Technology Stack
- ASP.NET Core 9
- Entity Framework Core
- SQL Server
- Bootstrap
- REST APIs / Microservices

---

## License
Copyright © 2025. All rights reserved.
This project is proprietary; copying, modifying, distributing, or using it requires prior written permission from the owner.
